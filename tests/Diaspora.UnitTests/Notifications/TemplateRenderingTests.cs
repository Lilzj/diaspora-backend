using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.Modules.Notifications.Infrastructure.DeepLinks;
using Diaspora.Modules.Notifications.Infrastructure.Templates;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// Template rendering: substitution, the fallback chain, culture and — most importantly — time zones.
/// </summary>
public class TemplateRenderingTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private readonly DefaultTemplateRenderer _renderer = new(new DefaultDeepLinkResolver());

    [Fact]
    public void Variables_are_substituted()
    {
        var rendered = Render(Template(), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["employername"] = "Kite Labs",
            ["scheduledat"] = Now
        });

        rendered.Title.Should().Be("Interview with Kite Labs");
    }

    /// <summary>
    /// <b>The single most consequential formatting rule in the module.</b> A member in Lagos reading
    /// "your interview is at 09:00" about a 10:00 interview has been given the wrong information by
    /// exactly one missing conversion.
    /// </summary>
    [Fact]
    public void A_time_renders_in_the_readers_own_zone()
    {
        var lagos = _renderer.Render(new TemplateRenderRequest
        {
            Template = Template(),
            Language = "en",
            TimeZoneId = "Africa/Lagos",
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employername"] = "Kite Labs",
                ["scheduledat"] = Now
            }
        });

        var utc = _renderer.Render(new TemplateRenderRequest
        {
            Template = Template(),
            Language = "en",
            TimeZoneId = "UTC",
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employername"] = "Kite Labs",
                ["scheduledat"] = Now
            }
        });

        lagos.IsSuccess.Should().BeTrue();
        utc.IsSuccess.Should().BeTrue();
        lagos.Value.Body.Should().NotBe(
            utc.Value.Body,
            "09:00 UTC is 10:00 in Lagos, and a member reading the wrong one turns up an hour late");
    }

    /// <summary>
    /// <c>pt-BR</c> with only a <c>pt</c> localization must render Portuguese, not English. This is
    /// the whole of the fallback chain and the reason it exists.
    /// </summary>
    [Fact]
    public void A_regional_tag_falls_back_to_its_neutral_parent()
    {
        var template = Template();

        template.SetLocalization(TemplateLocalization.Create(
            Guid.NewGuid(), "pt", "Entrevista com {employername}", "Sua entrevista é em {scheduledat}."));

        var rendered = _renderer.Render(new TemplateRenderRequest
        {
            Template = template,
            Language = "pt-BR",
            TimeZoneId = "UTC",
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employername"] = "Kite Labs",
                ["scheduledat"] = Now
            }
        });

        rendered.Value.Language.Should().Be("pt");
        rendered.Value.Title.Should().StartWith("Entrevista");
    }

    /// <summary>An unknown language falls all the way back to English rather than failing.</summary>
    [Fact]
    public void An_unmatched_language_falls_back_to_english()
    {
        var rendered = _renderer.Render(new TemplateRenderRequest
        {
            Template = Template(),
            Language = "yo",
            TimeZoneId = "UTC",
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employername"] = "Kite Labs",
                ["scheduledat"] = Now
            }
        });

        rendered.Value.Language.Should().Be("en");
    }

    /// <summary>
    /// A missing required value fails the render rather than producing "Interview with ". A message
    /// with a hole in it is one the member receives and cannot act on.
    /// </summary>
    [Fact]
    public void A_missing_required_variable_fails_the_render()
    {
        var rendered = _renderer.Render(new TemplateRenderRequest
        {
            Template = Template(),
            Language = "en",
            TimeZoneId = "UTC",
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["scheduledat"] = Now
            }
        });

        rendered.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// A value containing braces renders literally. A second substitution pass over inserted text is
    /// how a member's own words become a template injection.
    /// </summary>
    [Fact]
    public void A_value_containing_braces_is_not_substituted_again()
    {
        var rendered = Render(Template(), new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["employername"] = "{scheduledat}",
            ["scheduledat"] = Now
        });

        rendered.Title.Should().Be("Interview with {scheduledat}");
    }

    [Fact]
    public void A_plural_body_is_chosen_when_the_count_is_not_one()
    {
        var template = Template();

        template.SetLocalization(TemplateLocalization.Create(
            Guid.NewGuid(),
            "en",
            "Interview with {employername}",
            "You have one interview.",
            pluralBody: "You have several interviews."));

        var rendered = _renderer.Render(new TemplateRenderRequest
        {
            Template = template,
            Language = "en",
            TimeZoneId = "UTC",
            Count = 3,
            Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["employername"] = "Kite Labs",
                ["scheduledat"] = Now
            }
        });

        rendered.Value.Body.Should().Be("You have several interviews.");
    }

    private RenderedTemplate Render(
        NotificationTemplate template, IReadOnlyDictionary<string, object?> values)
    {
        var result = _renderer.Render(new TemplateRenderRequest
        {
            Template = template,
            Language = "en",
            TimeZoneId = "UTC",
            Values = values
        });

        result.IsSuccess.Should().BeTrue();

        return result.Value;
    }

    private static NotificationTemplate Template()
    {
        var template = NotificationTemplate.Draft(
            NotificationTemplateId.New(),
            "jobs.interview.scheduled",
            "jobs",
            NotificationPriority.High,
            Now,
            // Remediation Wave G: the client's route is `/job/[id]`, singular.
            routeTemplate: "/job/{jobid}").Value;

        template.SetVariables(
        [
            TemplateVariable.Create(Guid.NewGuid(), "employername"),
            TemplateVariable.Create(
                Guid.NewGuid(), "scheduledat", format: TemplateVariable.DateTimeFormat),
            TemplateVariable.Create(Guid.NewGuid(), "jobid", isRequired: false)
        ]);

        template.SetLocalization(TemplateLocalization.Create(
            Guid.NewGuid(),
            "en",
            "Interview with {employername}",
            "Your interview is on {scheduledat}."));

        return template;
    }
}
