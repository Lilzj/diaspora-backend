using System.Diagnostics.CodeAnalysis;

namespace Diaspora.Modules.Integrations.Domain.Providers;

/// <summary>
/// A provider identity, as <c>domain.provider</c>.
/// <para>
/// <b>The dotted form is the frontend's and is parsed rather than re-invented.</b>
/// <c>types/integrations.ts</c> declares the whole set — <c>calendar.google</c>,
/// <c>identity.apple</c>, <c>push.fcm</c> — and gives the reason the domain is part of the key:
/// "<c>calendar.apple</c> and <c>identity.apple</c> are different integrations that happen to share
/// a vendor". A key of <c>apple</c> would make one connection serve both, which is a scope
/// escalation with a friendly name.
/// </para>
/// <para>
/// <b>Validated on the way in, never on the way out.</b> A provider key arrives as a URL segment,
/// so it is attacker-controlled: it is length-capped, lower-cased and restricted to a character set
/// that cannot contain a path separator, a scheme or a null. Every persisted key has been through
/// here, which is what lets the read path treat the stored value as safe.
/// </para>
/// </summary>
public readonly record struct ProviderKey
{
    /// <summary>
    /// Long enough for every real key and short enough that a 4 KB URL cannot become a 4 KB column.
    /// </summary>
    public const int MaxLength = 64;

    private ProviderKey(string domain, string provider)
    {
        Domain = domain;
        Provider = provider;
    }

    /// <summary>The integration family — <c>calendar</c>, <c>identity</c>, <c>push</c>.</summary>
    public string Domain { get; }

    /// <summary>The vendor within it — <c>google</c>, <c>apple</c>, <c>device</c>.</summary>
    public string Provider { get; }

    /// <summary>The full dotted key, as it is stored and as the client sends it.</summary>
    public string Value => $"{Domain}.{Provider}";

    public override string ToString() => Value;

    /// <summary>
    /// Parses a client-supplied key, or fails.
    /// <para>
    /// Exactly one dot. Two would make <c>a.b.c</c> ambiguous between domain <c>a</c> and domain
    /// <c>a.b</c>, and picking either silently would mean two spellings of one connection.
    /// </para>
    /// </summary>
    public static bool TryParse(string? value, out ProviderKey key)
    {
        key = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().ToLowerInvariant();

        if (trimmed.Length > MaxLength)
        {
            return false;
        }

        var separator = trimmed.IndexOf('.', StringComparison.Ordinal);

        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var domain = trimmed[..separator];
        var provider = trimmed[(separator + 1)..];

        if (!IsSegment(domain) || !IsSegment(provider))
        {
            return false;
        }

        // Email and password is the only way to sign in (decided 5 Oct 2026), so no external
        // identity may be linked. Refused here, at the parse every route goes through, rather
        // than in `Domains.IsKnown` — which nothing calls, so removing `identity` from it alone
        // changed nothing.
        if (string.Equals(domain, RemovedIdentityDomain, StringComparison.Ordinal))
        {
            return false;
        }

        key = new ProviderKey(domain, provider);

        return true;
    }

    /// <summary>Parses, or throws. For call sites where the value came from this module.</summary>
    public static ProviderKey Parse(string value) =>
        TryParse(value, out var key)
            ? key
            : throw new ArgumentException($"'{value}' is not a provider key.", nameof(value));

    /// <summary>Whether the key names the calendar family.</summary>
    public bool IsCalendar => string.Equals(Domain, Domains.Calendar, StringComparison.Ordinal);

    /// <summary>
    /// Lower-case ASCII letters, digits and a hyphen. No dot, so a segment cannot contain the
    /// separator; no slash, so a key cannot escape a route; no underscore, because nothing in the
    /// client's set uses one and a narrower set is a smaller surface.
    /// </summary>
    private static bool IsSegment(ReadOnlySpan<char> segment)
    {
        if (segment.IsEmpty)
        {
            return false;
        }

        foreach (var character in segment)
        {
            var allowed = character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private const string RemovedIdentityDomain = "identity";

    /// <summary>The domain segments the frontend declares.</summary>
    public static class Domains
    {
        public const string Calendar = "calendar";
        public const string Push = "push";
        public const string Email = "email";
        public const string Sms = "sms";
        public const string Government = "government";
        public const string Maps = "maps";
        public const string Payments = "payments";

        /// <summary>Whether a parsed domain is one the client's union contains.</summary>
        public static bool IsKnown([NotNullWhen(true)] string? domain) => domain switch
        {
            // `identity` was removed on 5 Oct 2026: email and password is the only way to sign
            // in, so no `identity.*` key parses and nothing can link one.
            Calendar or Push or Email or Sms or Government or Maps or Payments => true,
            _ => false
        };
    }
}
