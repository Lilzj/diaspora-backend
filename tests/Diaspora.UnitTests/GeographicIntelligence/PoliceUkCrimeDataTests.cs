using System.Net;
using System.Text;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Infrastructure.Safety;
using Diaspora.Modules.GeographicIntelligence.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace Diaspora.UnitTests.GeographicIntelligence;

/// <summary>
/// The police.uk reader against a fake transport — 17 Sep 2026.
/// <para>
/// The responses are shaped as data.police.uk documents them. That is the weakest kind of evidence
/// (the documentation of a provider has been wrong on this platform before), so these tests pin the
/// rules this adapter adds on top — all months or none, a year of nothing is no coverage, the cache —
/// and the shape is checked against the live source before it is relied on.
/// </para>
/// </summary>
public sealed class PoliceUkCrimeDataTests
{
    private const string Dates =
        """[{"date":"2026-07","stop-and-search":[]},{"date":"2026-06","stop-and-search":[]},{"date":"2026-05","stop-and-search":[]},{"date":"2026-04","stop-and-search":[]}]""";

    private const string Categories =
        """[{"url":"all-crime","name":"All crime"},{"url":"anti-social-behaviour","name":"Anti-social behaviour"},{"url":"burglary","name":"Burglary"}]""";

    [Fact]
    public async Task Crimes_are_counted_per_category_over_the_most_recent_published_months()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ when path.Contains("date=2026-07", StringComparison.Ordinal) => Crimes("anti-social-behaviour", "burglary"),
            _ when path.Contains("date=2026-06", StringComparison.Ordinal) => Crimes("anti-social-behaviour"),
            _ when path.Contains("date=2026-05", StringComparison.Ordinal) => Crimes("burglary", "burglary", "vehicle-crime"),
            _ => null
        });

        var answer = await Reader(transport, months: 3).CountAsync(51.4742, -0.0691);

        answer.UnavailableReason.Should().BeNull();
        answer.Counts!.FirstMonth.Should().Be("2026-05");
        answer.Counts.LastMonth.Should().Be("2026-07");
        answer.Counts.Months.Should().Be(3);
        answer.Counts.ByCategory.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["anti-social-behaviour"] = 2,
            ["burglary"] = 3,
            ["vehicle-crime"] = 1
        });
        answer.Counts.Total.Should().Be(6);
        answer.Counts.CategoryNames["anti-social-behaviour"].Should().Be("Anti-social behaviour");

        transport.Requests.Should().NotContain(path => path.Contains("date=2026-04", StringComparison.Ordinal),
            "only the three most recent months are read");
        transport.Requests.Should().Contain(path => path.Contains("lat=51.474200&lng=-0.069100", StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_unreadable_month_makes_the_whole_answer_unreadable_rather_than_a_shorter_period()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ when path.Contains("date=2026-06", StringComparison.Ordinal) => null,
            _ => Crimes("burglary")
        });

        var answer = await Reader(transport, months: 3).CountAsync(51.4742, -0.0691);

        answer.Counts.Should().BeNull();
        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.SourceUnavailable);
    }

    [Fact]
    public async Task A_year_without_a_single_crime_is_no_coverage_not_a_safe_place()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ => "[]"
        });

        var answer = await Reader(transport, months: 3).CountAsync(55.9533, -3.1883);

        answer.Counts.Should().BeNull();
        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.NoCoverage);
    }

    [Fact]
    public async Task A_disabled_reader_calls_nothing()
    {
        var transport = new FakeTransport(_ => "[]");

        var answer = await Reader(transport, months: 3, enabled: false).CountAsync(51.4742, -0.0691);

        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.Disabled);
        transport.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_month_already_counted_for_a_point_is_not_read_again()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ => Crimes("burglary")
        });

        var reader = Reader(transport, months: 2);

        await reader.CountAsync(51.4742, -0.0691);
        var first = transport.Requests.Count;

        await reader.CountAsync(51.4742, -0.0691);

        transport.Requests.Count.Should().Be(first, "the dates, the names and both months are cached");
        first.Should().Be(4);
    }

    [Fact]
    public void Only_england_wales_and_northern_ireland_are_covered()
    {
        var reader = Reader(new FakeTransport(_ => "[]"), months: 1);

        reader.Covers("GB").Should().BeTrue();
        reader.Covers("MU").Should().BeFalse();
        reader.Covers("NG").Should().BeFalse();
    }

    // ── The heat layer's viewport read (21 Oct 2026) ─────────────────────────────────────────────

    [Fact]
    public async Task A_viewport_is_read_by_polygon_for_the_months_that_overlap_the_window()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ when path.Contains("date=2026-07", StringComparison.Ordinal) => Crimes("burglary", "anti-social-behaviour"),
            _ when path.Contains("date=2026-06", StringComparison.Ordinal) => Crimes("burglary"),
            _ => "[]"
        });

        var answer = await Reader(transport, months: 12).ListInBoxAsync(
            51.46, -0.10, 51.49, -0.04,
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

        answer.UnavailableReason.Should().BeNull();
        answer.FirstMonth.Should().Be("2026-06");
        answer.LastMonth.Should().Be("2026-07");
        answer.Months.Should().Be(2);
        answer.Points.Should().HaveCount(3);
        answer.Points.Should().AllSatisfy(point =>
        {
            point.Latitude.Should().Be(51.474, "the source sends coordinates as strings");
            point.Longitude.Should().Be(-0.069);
        });

        transport.Requests.Should().Contain(path =>
            path.StartsWith("crimes-street/all-crime?poly=51.49000,-0.10000:51.49000,-0.04000:51.46000,-0.04000:51.46000,-0.10000", StringComparison.Ordinal));
        transport.Requests.Should().NotContain(path => path.Contains("date=2026-05", StringComparison.Ordinal),
            "May ends before the window starts");
    }

    [Fact]
    public async Task A_window_after_the_newest_published_month_is_not_yet_published_rather_than_zero()
    {
        var transport = new FakeTransport(path => path == "crimes-street-dates" ? Dates : "[]");

        var answer = await Reader(transport, months: 12).ListInBoxAsync(
            51.46, -0.10, 51.49, -0.04,
            new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero));

        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.NotYetPublished);
        answer.NewestPublishedMonth.Should().Be("2026-07");
        transport.Requests.Should().NotContain(path => path.StartsWith("crimes-street/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_a_window_only_the_newest_month_is_read()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ => Crimes("burglary")
        });

        var answer = await Reader(transport, months: 12).ListInBoxAsync(51.46, -0.10, 51.49, -0.04, null, null);

        answer.Months.Should().Be(1);
        answer.LastMonth.Should().Be("2026-07");
        transport.Requests.Count(path => path.StartsWith("crimes-street/", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact]
    public async Task An_area_the_source_refuses_as_too_large_is_said_so()
    {
        var transport = new FakeTransport(path => path switch
        {
            "crimes-street-dates" => Dates,
            _ when path.StartsWith("crime-categories", StringComparison.Ordinal) => Categories,
            _ => null
        });

        var answer = await Reader(transport, months: 12).ListInBoxAsync(51.0, -0.5, 52.0, 0.5, null, null);

        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.TooManyToRead);
        answer.Points.Should().BeEmpty();
    }

    [Fact]
    public async Task A_disabled_source_lists_nothing_and_calls_nobody()
    {
        var transport = new FakeTransport(_ => "[]");

        var answer = await Reader(transport, months: 12, enabled: false).ListInBoxAsync(51.46, -0.10, 51.49, -0.04, null, null);

        answer.UnavailableReason.Should().Be(PoliceCrimeUnavailable.Disabled);
        transport.Requests.Should().BeEmpty();
    }

    private static PoliceUkCrimeData Reader(FakeTransport transport, int months, bool enabled = true) =>
        new(
            new FakeClients(transport),
            OptionsFactory.Create(new PoliceUkOptions
            {
                Enabled = enabled,
                Months = months,
                BaseAddress = "https://data.police.uk/api/",
                RequestTimeoutSeconds = 5,
                MaxConcurrentRequests = 2
            }),
            TimeProvider.System,
            NullLogger<PoliceUkCrimeData>.Instance);

    private static string Crimes(params string[] categories) =>
        "[" + string.Join(",", categories.Select((category, index) =>
            $$"""{"category":"{{category}}","location_type":"Force","location":{"latitude":"51.474","street":{"id":{{index}},"name":"On or near High Street"},"longitude":"-0.069"},"context":"","outcome_status":null,"persistent_id":"","id":{{index}},"location_subtype":"","month":"2026-07"}""")) + "]";

    private sealed class FakeClients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Answers each path from a function; null is a 503, as the source answers an oversized area.</summary>
    private sealed class FakeTransport(Func<string, string?> answer) : HttpMessageHandler
    {
        private readonly List<string> _requests = [];

        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_requests)
                {
                    return [.. _requests];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery["/api/".Length..];

            lock (_requests)
            {
                _requests.Add(path);
            }

            var body = answer(path);

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
        }
    }
}
