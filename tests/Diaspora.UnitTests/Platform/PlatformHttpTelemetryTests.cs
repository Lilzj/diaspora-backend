using System.Diagnostics.Metrics;
using System.Reflection;
using Diaspora.Platform.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// The instrument three of the platform's five launch alerts read.
/// <para>
/// <b>Asserted through a <see cref="MeterListener"/> rather than against a classifier.</b> What is
/// worth knowing is that a measurement actually leaves the instrument with the tags an alert will
/// filter on, and a test of the private classification would pass just as happily if nothing ever
/// called it. The listener also covers the call site, which is the half that silently rots:
/// <see cref="PlatformProblem"/> could stop recording and every classification test would stay
/// green.
/// </para>
/// <para>
/// Assertions use <c>Contain</c> rather than <c>ContainSingle</c> and unique codes where a count
/// matters, because xUnit runs test classes in parallel and this meter is process-wide — another
/// class building a problem document would otherwise fail this one, intermittently.
/// </para>
/// </summary>
public class PlatformHttpTelemetryTests
{
    [Fact]
    public void A_platform_code_is_recorded_under_its_own_name()
    {
        Capture(() => PlatformHttpTelemetry.Record(PlatformErrorCodes.Unexpected, 500))
            .Should().Contain(m =>
                m.Code == PlatformErrorCodes.Unexpected && m.Status == "500" && m.Value == 1);
    }

    /// <summary>
    /// The cardinality bound, which is the design rather than a detail: roughly five hundred and
    /// ninety domain codes exist, and one instrument that fires on every error response cannot
    /// carry a dimension that wide.
    /// </summary>
    [Fact]
    public void A_domain_code_collapses_to_one_value()
    {
        var measurements = Capture(
            () => PlatformHttpTelemetry.Record("unittests.alpha.not_found", 404));

        measurements.Should().Contain(m =>
            m.Code == PlatformHttpTelemetry.DomainCode && m.Status == "404");

        measurements.Should().NotContain(m => m.Code == "unittests.alpha.not_found",
            "the raw code is the dimension that would mint a time series per distinct failure");
    }

    /// <summary>
    /// <b>The test that earns the reflective set.</b> <c>StepUpRequired</c> is
    /// <c>Identity.StepUp.Required</c>, so the obvious implementation —
    /// <c>code.StartsWith("Platform.")</c> — would file the platform's own step-up refusal under
    /// somebody else's domain and lose the one code a client is meant to act on.
    /// </summary>
    [Fact]
    public void The_step_up_code_is_recognised_although_it_does_not_start_with_platform()
    {
        PlatformErrorCodes.StepUpRequired.Should().NotStartWith("Platform.",
            "if this ever changes, the point this test is making has moved");

        Capture(() => PlatformHttpTelemetry.Record(PlatformErrorCodes.StepUpRequired, 403))
            .Should().Contain(m => m.Code == PlatformErrorCodes.StepUpRequired);
    }

    /// <summary>
    /// Every platform code, not a sample. A code added to the class and not to the set would be
    /// indistinguishable from a domain failure on every dashboard, which is the quiet half of
    /// BARCH-008 reappearing one code at a time.
    /// </summary>
    [Fact]
    public void Every_platform_error_code_keeps_its_own_tag_value()
    {
        var codes = typeof(PlatformErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        codes.Should().HaveCountGreaterThanOrEqualTo(12,
            "finding none would make the loop below assert nothing at all");

        foreach (var code in codes)
        {
            Capture(() => PlatformHttpTelemetry.Record(code, 400))
                .Should().Contain(m => m.Code == code, "{0} is a platform code", code);
        }
    }

    /// <summary>
    /// The call site. Building a problem document is what every failure path on the platform does,
    /// and it is what has to produce the measurement.
    /// </summary>
    [Fact]
    public void Building_a_problem_document_records_a_measurement()
    {
        var measurements = Capture(() => PlatformProblem.Create(
            new DefaultHttpContext(),
            StatusCodes.Status503ServiceUnavailable,
            PlatformErrorCodes.ServiceUnavailable,
            "Service unavailable",
            "A dependency is down."));

        measurements.Should().Contain(m =>
            m.Code == PlatformErrorCodes.ServiceUnavailable && m.Status == "503");
    }

    /// <summary>
    /// The second factory. Validation problems are built by a different method with a different
    /// type, and they converge on the same enrichment -- which is the reason the measurement lives
    /// there rather than in either entry point.
    /// </summary>
    [Fact]
    public void Building_a_validation_problem_records_a_measurement_too()
    {
        var measurements = Capture(() => PlatformProblem.CreateValidation(
            new DefaultHttpContext(),
            new Dictionary<string, string[]> { ["name"] = ["Required."] },
            "Validation failed."));

        measurements.Should().Contain(m =>
            m.Code == PlatformErrorCodes.Validation && m.Status == "400");
    }

    /// <summary>
    /// A status the caller never set still produces a measurement rather than an exception. The
    /// instrument is on the failure path, where throwing would turn the failure into a worse one.
    /// </summary>
    [Fact]
    public void A_missing_status_is_recorded_as_zero_rather_than_thrown()
    {
        var record = () => PlatformHttpTelemetry.Record(null, 0);

        record.Should().NotThrow();

        Capture(() => PlatformHttpTelemetry.Record(null, 0))
            .Should().Contain(m => m.Code == PlatformHttpTelemetry.DomainCode && m.Status == "0");
    }

    private static List<Measurement> Capture(Action act)
    {
        var captured = new List<Measurement>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, subscriber) =>
        {
            if (instrument.Meter.Name == PlatformHttpTelemetry.SourceName)
            {
                subscriber.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? code = null;
            string? status = null;

            foreach (var tag in tags)
            {
                if (string.Equals(tag.Key, "code", StringComparison.Ordinal))
                {
                    code = tag.Value as string;
                }
                else if (string.Equals(tag.Key, "status", StringComparison.Ordinal))
                {
                    status = tag.Value as string;
                }
            }

            captured.Add(new Measurement(value, code, status));
        });

        listener.Start();

        act();

        return captured;
    }

    private sealed record Measurement(long Value, string? Code, string? Status);
}
