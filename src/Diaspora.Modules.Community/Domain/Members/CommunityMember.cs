using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Members;

/// <summary>
/// The fact that a member exists, held by Community itself.
/// <para>
/// <b>BG-05a, and the whole point of it.</b> Reporting a <em>person</em> is the one moderation
/// subject this platform could not accept, and the reason was never product intent — it was that
/// no module which may talk to Community could answer "does this member exist". A content source
/// for <see cref="Enums.CommunityContentType.Member"/> was written in Trust, worked at runtime, and
/// was reverted because the architecture tests correctly refused it on two counts: Trust may not
/// depend on Identity's domain (ADR-002), and Trust composes at 200 while Community composes at 500.
/// Neither rule was weakened then and neither is weakened now.
/// </para>
/// <para>
/// <b>This is the second of the two designs the reconciliation brief named as acceptable</b> — a
/// Community-owned projection fed by <c>UserRegisteredIntegrationEvent</c> — and it was chosen over
/// a member-existence method on a kernel contract for one reason: the kernel contract Community may
/// already use is <c>ITrustService</c>, whose subject is trust. Existence is not a trust question,
/// and hanging it there would put a second unrelated responsibility on a contract every module
/// reads.
/// </para>
/// <para>
/// <b>Nothing is reached into.</b> <c>UserRegisteredIntegrationEvent</c> lives in
/// <c>Diaspora.Contracts</c>, which Community already references; the rule the tests enforce forbids
/// <c>Diaspora.Domain.Identity</c>, the domain, and that is untouched. This is the shape the
/// registration contract's own summary asks for in as many words: <i>"other modules use this to seed
/// their own read models; they must not query Identity's tables directly."</i> Nothing had, until
/// now, and this is the first.
/// </para>
/// <para>
/// <b>Deliberately three fields.</b> A projection that grew a phone number or an email would become
/// a second copy of the person, and the module that holds the moderation queue is the last place a
/// duplicate of the identity record belongs. What a report needs is that the subject is real and has
/// a name to render — no more than that.
/// </para>
/// <para>
/// <b>The first attempt at this got existence wrong one layer down</b>, and it is recorded here
/// because the failure is easy to repeat. It asked <c>ITrustProfileRepository</c>, assuming every
/// member has a trust profile; they do not — one is created on first trust interaction — so
/// reporting somebody who registered ten minutes ago answered <c>community.content.not-found</c>.
/// That is the exact failure the class existed to remove, reintroduced, and it landed hardest on the
/// newest accounts, which are the ones most likely to be worth reporting. Registration is the only
/// event every member has.
/// </para>
/// </summary>
public sealed class CommunityMember
{
    private CommunityMember(
        UserId userId, string displayName, string? countryCode, DateTimeOffset registeredAt)
    {
        UserId = userId;
        DisplayName = displayName;
        CountryCode = countryCode;
        RegisteredAt = registeredAt;
    }

    private CommunityMember() { }

    /// <summary>The member. Also the key — a member appears here once or not at all.</summary>
    public UserId UserId { get; private set; } = default!;

    /// <summary>
    /// What a moderator sees in the queue instead of a bare identifier.
    /// <para>
    /// Kept current by <c>UserProfileUpdatedIntegrationEvent</c> where one is published, and stale
    /// otherwise. A stale display name in a moderation queue is a cosmetic problem; the report it is
    /// attached to still points at the right person, because it points by id.
    /// </para>
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2. Null when registration did not carry one.</summary>
    public string? CountryCode { get; private set; }

    public DateTimeOffset RegisteredAt { get; private set; }

    public static CommunityMember Project(
        UserId userId, string? displayName, string? countryCode, DateTimeOffset registeredAt) =>
        new(userId, Name(displayName), Trimmed(countryCode), registeredAt);

    /// <summary>
    /// Refreshes the rendered name. Returns whether anything actually moved, so a redelivered
    /// registration writes nothing.
    /// </summary>
    public bool Refresh(string? displayName, string? countryCode)
    {
        var name = Name(displayName);
        var country = Trimmed(countryCode);

        if (string.Equals(name, DisplayName, StringComparison.Ordinal)
            && string.Equals(country, CountryCode, StringComparison.Ordinal))
        {
            return false;
        }

        DisplayName = name;
        CountryCode = country;
        return true;
    }

    /// <summary>
    /// A member with no name renders as "Member" rather than as an empty string, because an empty
    /// cell in a moderation queue reads as a broken row rather than as a missing name.
    /// </summary>
    private static string Name(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Member" : value.Trim();

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
