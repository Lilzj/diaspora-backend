using System.Globalization;
using System.Text;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.Modules.Notifications.Extensibility.DeepLinks;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Infrastructure.Templates;

/// <summary>
/// Substitutes <c>{token}</c> holes, in the reader's culture and time zone.
/// <para>
/// <b>Substitution and formatting, and deliberately nothing else.</b> No conditionals, no loops, no
/// property paths, no expressions. Everything a template author can write either renders or is
/// caught by publication validation, which means reference data cannot fail at three in the morning
/// in a way that costs somebody an interview.
/// </para>
/// <para>
/// <b>The culture and the zone are the reader's, and both matter more than they look.</b> A member
/// in Lagos reading "your interview is at 09:00" about a 10:00 interview is the worst bug this
/// module could ship, and it is one missing time-zone conversion away. A member in France reading a
/// price as <c>1,250.00</c> reads one thousand two hundred and fifty as one and a quarter.
/// </para>
/// <para>
/// <b>An unknown culture formats invariantly rather than failing.</b> A member whose language tag
/// the runtime does not recognise still gets their message, in slightly wrong-looking numbers.
/// </para>
/// </summary>
public sealed class DefaultTemplateRenderer : ITemplateRenderer
{
    private readonly IDeepLinkResolver _deepLinks;

    public DefaultTemplateRenderer(IDeepLinkResolver deepLinks) => _deepLinks = deepLinks;

    public Result<RenderedTemplate> Render(TemplateRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = request.Template;
        var localization = template.ResolveLocalization(request.Language);

        if (localization is null)
        {
            return Result.Failure<RenderedTemplate>(NotificationErrors.TemplateLocaleMissing);
        }

        var variant = request.Channel is { } channel
            ? template.ResolveVariant(channel, localization.Language)
            : null;

        var culture = CultureFor(localization.Language);
        var zone = ZoneFor(request.TimeZoneId);

        var missing = FindMissingRequired(template, request.Values);

        if (missing is not null)
        {
            return Result.Failure<RenderedTemplate>(NotificationErrors.TemplateVariableUnknown);
        }

        var title = Substitute(
            variant?.Title ?? localization.Title, template, request.Values, culture, zone);

        var body = Substitute(
            variant?.Body ?? localization.BodyFor(request.Count),
            template, request.Values, culture, zone);

        var shortBody = localization.ShortBody is { } authored
            ? Substitute(authored, template, request.Values, culture, zone)
            : null;

        var route = _deepLinks
            .Resolve(template.RouteTemplate, request.Values)
            ?.Route;

        return Result.Success(new RenderedTemplate
        {
            Title = title,
            Body = body,
            ShortBody = shortBody,
            Language = localization.Language,
            Format = variant?.Format ?? localization.Format,
            Route = route,
            Actions = RenderActions(template, request.Values, culture, zone)
        });
    }

    private List<RenderedAction> RenderActions(
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> values,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        var rendered = new List<RenderedAction>(template.Actions.Count);

        foreach (var action in template.Actions)
        {
            rendered.Add(new RenderedAction
            {
                Key = action.Key,
                Label = Substitute(action.Label, template, values, culture, zone),
                Route = _deepLinks.Resolve(action.RouteTemplate, values)?.Route,
                Style = action.Style
            });
        }

        return rendered;
    }

    /// <summary>
    /// The name of the first required variable with no value, or null when everything is present.
    /// <para>
    /// An empty string counts as absent. "Your interview with  is confirmed" is a message nobody
    /// should receive, and a caller that genuinely means blank can pass a space.
    /// </para>
    /// </summary>
    private static string? FindMissingRequired(
        NotificationTemplate template, IReadOnlyDictionary<string, object?> values)
    {
        foreach (var variable in template.Variables)
        {
            if (!variable.IsRequired)
            {
                continue;
            }

            if (!values.TryGetValue(variable.Name, out var value)
                || value is null
                || (value is string text && string.IsNullOrEmpty(text)))
            {
                return variable.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks the string once, replacing tokens.
    /// <para>
    /// A single pass rather than a replace-per-variable loop, which would be quadratic in the number
    /// of declared variables and — worse — would substitute into text a previous substitution had
    /// just inserted. A value containing <c>{name}</c> must render as those seven characters, not
    /// become a second lookup.
    /// </para>
    /// </summary>
    private static string Substitute(
        string text,
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> values,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('{', StringComparison.Ordinal))
        {
            return text;
        }

        var output = new StringBuilder(text.Length + 32);
        var token = new StringBuilder(32);
        var inToken = false;

        foreach (var character in text)
        {
            if (character == '{')
            {
                // A second brace abandons what was being read and renders it literally.
                if (inToken)
                {
                    output.Append('{').Append(token);
                }

                inToken = true;
                token.Clear();
                continue;
            }

            if (character == '}' && inToken)
            {
                output.Append(Format(token.ToString(), template, values, culture, zone));
                inToken = false;
                token.Clear();
                continue;
            }

            if (inToken)
            {
                token.Append(character);
            }
            else
            {
                output.Append(character);
            }
        }

        // An unclosed brace renders literally rather than swallowing the rest of the message.
        if (inToken)
        {
            output.Append('{').Append(token);
        }

        return output.ToString();
    }

    /// <summary>
    /// One token's value, formatted the way its declaration asks.
    /// <para>
    /// An undeclared token renders as an empty string rather than as its own name. Publication
    /// validation catches undeclared tokens, so reaching here means an operator edited a row past
    /// the validator — and a message with a blank in it is less alarming to a member than one
    /// containing <c>{employerName}</c>.
    /// </para>
    /// </summary>
    private static string Format(
        string rawToken,
        NotificationTemplate template,
        IReadOnlyDictionary<string, object?> values,
        CultureInfo culture,
        TimeZoneInfo zone)
    {
        var name = rawToken.Trim().ToLowerInvariant();

        if (!values.TryGetValue(name, out var value) || value is null)
        {
            return string.Empty;
        }

        var declared = template.Variables
            .FirstOrDefault(variable =>
                string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase));

        return declared?.Format switch
        {
            TemplateVariable.DateFormat => FormatDate(value, culture, zone),
            TemplateVariable.TimeFormat => FormatTime(value, culture, zone),
            TemplateVariable.DateTimeFormat => FormatDateTime(value, culture, zone),
            TemplateVariable.CurrencyFormat => FormatCurrency(value, culture),
            TemplateVariable.NumberFormat => FormatNumber(value, culture),
            _ => Convert.ToString(value, culture) ?? string.Empty
        };
    }

    private static string FormatDate(object value, CultureInfo culture, TimeZoneInfo zone) =>
        AsLocal(value, zone) is { } local
            ? local.ToString("D", culture)
            : Convert.ToString(value, culture) ?? string.Empty;

    private static string FormatTime(object value, CultureInfo culture, TimeZoneInfo zone) =>
        AsLocal(value, zone) is { } local
            ? local.ToString("t", culture)
            : Convert.ToString(value, culture) ?? string.Empty;

    private static string FormatDateTime(object value, CultureInfo culture, TimeZoneInfo zone) =>
        AsLocal(value, zone) is { } local
            ? local.ToString("f", culture)
            : Convert.ToString(value, culture) ?? string.Empty;

    /// <summary>
    /// Money in the reader's culture.
    /// <para>
    /// A value written as <c>"1250 GBP"</c> keeps its own currency symbol and takes the reader's
    /// grouping — because the amount is a fact about a listing and the punctuation is a fact about
    /// the reader. Formatting a British price with a naira sign because the reader is Nigerian would
    /// be a far worse error than an unfamiliar separator.
    /// </para>
    /// </summary>
    private static string FormatCurrency(object value, CultureInfo culture)
    {
        if (value is string composite && TrySplitMoney(composite, out var amount, out var currency))
        {
            return string.Concat(amount.ToString("N2", culture), " ", currency);
        }

        return value is IConvertible convertible
            ? convertible.ToDecimal(culture).ToString("C", culture)
            : Convert.ToString(value, culture) ?? string.Empty;
    }

    private static string FormatNumber(object value, CultureInfo culture) =>
        value is IConvertible convertible
            ? convertible.ToDecimal(culture).ToString("N0", culture)
            : Convert.ToString(value, culture) ?? string.Empty;

    private static bool TrySplitMoney(string composite, out decimal amount, out string currency)
    {
        amount = 0;
        currency = string.Empty;

        var parts = composite.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return false;
        }

        if (!decimal.TryParse(
                parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        currency = parts[1];

        return true;
    }

    private static DateTimeOffset? AsLocal(object value, TimeZoneInfo zone) => value switch
    {
        DateTimeOffset offset => TimeZoneInfo.ConvertTime(offset, zone),
        DateTime dateTime => TimeZoneInfo.ConvertTime(new DateTimeOffset(dateTime.ToUniversalTime()), zone),
        string text when DateTimeOffset.TryParse(
            text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            => TimeZoneInfo.ConvertTime(parsed, zone),
        _ => null
    };

    /// <summary>The reader's culture, or the invariant one when the runtime does not know the tag.</summary>
    private static CultureInfo CultureFor(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(LocaleFallbackChain.Normalize(language));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>The reader's zone, or UTC. A stale zone identifier must not cost the message.</summary>
    private static TimeZoneInfo ZoneFor(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
}
