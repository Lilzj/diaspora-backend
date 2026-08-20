using System.Globalization;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Validation;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Infrastructure.Identity.Localization;

/// <summary>
/// Resolves message keys against the catalogue, using the language the request declared.
/// <para>
/// Language comes from the platform's <c>IRequestContext</c>, which reads it from the
/// request. Members whose account language differs from their device language still get
/// the device's language here, which is the right answer for validation errors — they are
/// reading them right now, on that device.
/// </para>
/// </summary>
public sealed class LocalizedMessageProvider : ILocalizedMessageProvider
{
    private readonly IRequestContext _requestContext;

    public LocalizedMessageProvider(IRequestContext requestContext)
    {
        _requestContext = requestContext;
    }

    public string GetMessage(string key) => IdentityMessageCatalog.Resolve(key, CurrentLanguage);

    public string GetMessage(string key, params object[] arguments)
    {
        var template = GetMessage(key);

        if (arguments.Length == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException)
        {
            // A translation with a malformed placeholder must not take down the request.
            // Returning the unformatted template still tells the member what is wrong.
            return template;
        }
    }

    public string GetFor(string key, string? language) => IdentityMessageCatalog.Resolve(key, language);

    private string CurrentLanguage =>
        _requestContext.Language
        ?? _requestContext.Culture
        ?? IdentityMessageCatalog.DefaultLanguage;
}
