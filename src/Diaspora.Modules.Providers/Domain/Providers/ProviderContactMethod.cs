using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Providers;

/// <summary>
/// One way to reach a provider.
/// <para>
/// A typed list where the client has four named properties (<c>phone</c>, <c>whatsApp</c>,
/// <c>website</c>, <c>email</c>), because the brief requires social links and a provider with
/// two phone numbers — a shop line and a mobile — is entirely ordinary. Four columns would have
/// meant a schema change per network for as long as networks keep being invented.
/// </para>
/// <para>
/// <see cref="IsPrimary"/> is what preserves the client's current behaviour: the provider screen
/// has one "Call" button, and it needs to know which number that is without the client picking
/// arbitrarily from a list.
/// </para>
/// <para>
/// Validation is shape-only and deliberately loose. A phone number here spans every dialling
/// convention on earth, and a regular expression strict enough to reject a bad UK mobile
/// rejects a good Ghanaian landline. What is checked is that the value is present, is not
/// absurdly long, and — for a website — parses as an absolute HTTP URL, because that one is fed
/// straight into <c>Linking.openURL</c> on the client and a malformed value there is a crash
/// rather than a wasted tap.
/// </para>
/// </summary>
public sealed class ProviderContactMethod : Entity<ProviderContactMethodId>
{
    public const int MaxValueLength = 512;

    public const int MaxLabelLength = 80;

    private ProviderContactMethod(
        ProviderContactMethodId id, ContactMethodKind kind, string value, string? label)
        : base(id)
    {
        Kind = kind;
        Value = value;
        Label = label;
    }

    private ProviderContactMethod() { }

    public ContactMethodKind Kind { get; private set; }

    /// <summary>The number, address, handle or URL. Never empty except for <c>PlatformMessage</c>.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>What the provider calls it. "Bookings", "Emergency line", "Arabic desk".</summary>
    public string? Label { get; private set; }

    /// <summary>The one the client's single action button uses, per kind.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>
    /// Whether this may be shown to anyone. A provider that will take messages but not calls
    /// from the public keeps its number here and hides it, and the platform message route
    /// remains open.
    /// </summary>
    public bool IsPublic { get; private set; } = true;

    public int Position { get; private set; }

    public static Result<ProviderContactMethod> Create(
        ProviderContactMethodId id,
        ContactMethodKind kind,
        string? value,
        string? label = null,
        bool isPrimary = false,
        bool isPublic = true,
        int position = 0)
    {
        ArgumentNullException.ThrowIfNull(id);

        // The in-platform route addresses the provider itself; there is nothing to store.
        if (kind == ContactMethodKind.PlatformMessage)
        {
            return Result.Success(new ProviderContactMethod(id, kind, string.Empty, Trim(label, MaxLabelLength))
            {
                IsPrimary = isPrimary,
                IsPublic = isPublic,
                Position = position < 0 ? 0 : position
            });
        }

        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxValueLength)
        {
            return Result.Failure<ProviderContactMethod>(ProviderErrors.ContactMethodValueInvalid);
        }

        var normalized = Normalize(kind, trimmed);

        if (normalized is null)
        {
            return Result.Failure<ProviderContactMethod>(ProviderErrors.ContactMethodValueInvalid);
        }

        return Result.Success(new ProviderContactMethod(id, kind, normalized, Trim(label, MaxLabelLength))
        {
            IsPrimary = isPrimary,
            IsPublic = isPublic,
            Position = position < 0 ? 0 : position
        });
    }

    /// <summary>Internal: only the aggregate may decide which method is primary for a kind.</summary>
    internal void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void SetPosition(int position) => Position = position < 0 ? 0 : position;

    /// <summary>
    /// A comparison key for duplicate detection. Kind plus normalised value, so the same number
    /// entered as a phone and as a WhatsApp is two methods (which is correct — they are two
    /// buttons) but the same number entered twice as a phone is one.
    /// </summary>
    public string DedupeKey => $"{(int)Kind}:{Value.ToUpperInvariant()}";

    private static string? Normalize(ContactMethodKind kind, string value) => kind switch
    {
        ContactMethodKind.Phone or ContactMethodKind.WhatsApp => NormalizePhone(value),
        ContactMethodKind.Email => value.Contains('@', StringComparison.Ordinal) ? value.ToLowerInvariant() : null,
        ContactMethodKind.Website => NormalizeUrl(value),
        _ => value
    };

    /// <summary>
    /// Strips formatting so <c>+44 7700 900456</c> and <c>+447700900456</c> are one number.
    /// No country inference — the platform has members in every dialling plan there is, and
    /// guessing a country code is how a Ghanaian number becomes an unreachable UK one.
    /// </summary>
    private static string? NormalizePhone(string value)
    {
        var digits = new string(value.Where(character => char.IsAsciiDigit(character) || character == '+').ToArray());

        return digits.Length is >= 5 and <= 20 ? digits : null;
    }

    private static string? NormalizeUrl(string value)
    {
        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";

        return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
