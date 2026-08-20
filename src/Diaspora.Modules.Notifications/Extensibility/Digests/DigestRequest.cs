using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Digests;

/// <summary>What to summarise, for whom, over what.</summary>
public sealed record DigestRequest
{
    public required UserId RecipientUserId { get; init; }

    public required DigestFrequency Frequency { get; init; }

    public required DateTimeOffset WindowStart { get; init; }

    public required DateTimeOffset WindowEnd { get; init; }

    /// <summary>Restricts the digest to one subject area. Null covers everything.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>Tags a country digest. Carried through to the published event; never filters.</summary>
    public string? CountryCode { get; init; }

    /// <summary>The language to assemble in. Falls back to the member's preference.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Whether a window with nothing in it still produces a record.
    /// <para>
    /// True from the sweep, so "why do I never get my daily summary" has an answer. False from a
    /// member pressing a button, who would rather be told there is nothing than be handed an empty
    /// page.
    /// </para>
    /// </summary>
    public bool RecordWhenEmpty { get; init; } = true;
}
