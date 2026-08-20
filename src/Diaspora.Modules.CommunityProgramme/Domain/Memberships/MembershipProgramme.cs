using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Memberships;

/// <summary>
/// What an organization offers the people who join it: the tiers, their prices, their benefits and
/// what each asks of an applicant.
/// <para>
/// <b>One per organization, and the organization itself is B12's.</b> This aggregate holds an
/// organization identifier and nothing else about the body — no name, no logo, no verification, no
/// category. Every one of those is read through B12's <c>IOrganizationDirectory</c> at the point of
/// rendering, so an organization renamed in B12 is renamed on its membership page in the same
/// instant, and there is no copy here to go stale.
/// </para>
/// <para>
/// <b>Tiers are owned rows rather than their own aggregate.</b> A programme has a handful of them,
/// they are always read together with the programme, and a tier without its programme is not a
/// thing. The counter that moves independently — <see cref="MembershipTier.FilledCount"/> — is
/// reconciled from a recount rather than incremented blindly, for the reason B12's sweep gives: a
/// delta compounds whatever drift it inherited, and a recount is idempotent.
/// </para>
/// <para>
/// <b>Not soft-deletable.</b> A programme that vanished would orphan every membership sold under it,
/// and a member holding a paid membership of a programme nobody can read has no way to see what they
/// bought. Closing intake is <see cref="IsOpen"/>; withdrawing from view is
/// <see cref="ProgrammeVisibility"/>. Neither erases anything.
/// </para>
/// </summary>
public sealed class MembershipProgramme
    : AggregateRoot<MembershipProgrammeId>, IAuditableEntity
{
    private readonly List<MembershipTier> _tiers = [];

    private MembershipProgramme(
        MembershipProgrammeId id,
        Guid organizationId,
        string summary,
        ProgrammeVisibility visibility,
        bool isOpen,
        bool requiresApproval,
        DateTimeOffset now) : base(id)
    {
        OrganizationId = organizationId;
        Summary = summary;
        Visibility = visibility;
        IsOpen = isOpen;
        RequiresApproval = requiresApproval;
        CreatedAt = now;
    }

    private MembershipProgramme() { }

    /// <summary>B12's organization. Uniquely indexed: one programme per body.</summary>
    public Guid OrganizationId { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public ProgrammeVisibility Visibility { get; private set; }

    /// <summary>False when the organization has closed intake entirely, whatever the tiers say.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Whether a human reviews an application before it can activate.</summary>
    public bool RequiresApproval { get; private set; }

    /// <summary>
    /// How many memberships are currently active or in their renewal grace period.
    /// <para>
    /// Reconciled from a recount by the lifecycle sweep, never accumulated from deltas.
    /// </para>
    /// </summary>
    public int MemberCount { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<MembershipTier> Tiers =>
        _tiers.OrderBy(tier => tier.SortOrder).ThenBy(tier => tier.Name, StringComparer.Ordinal)
            .ToList().AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation and editing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<MembershipProgramme> Open(
        MembershipProgrammeId id,
        Guid organizationId,
        string summary,
        ProgrammeVisibility visibility,
        bool isOpen,
        bool requiresApproval,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (organizationId == Guid.Empty)
        {
            return Result.Failure<MembershipProgramme>(
                CommunityProgrammeErrors.OrganizationNotFound);
        }

        return Result.Success(new MembershipProgramme(
            id, organizationId, Clip(summary, CommunityProgrammeLimits.MaxSummaryLength) ?? string.Empty,
            visibility, isOpen, requiresApproval, now));
    }

    public void Describe(
        string summary,
        ProgrammeVisibility visibility,
        bool isOpen,
        bool requiresApproval,
        DateTimeOffset now)
    {
        Summary = Clip(summary, CommunityProgrammeLimits.MaxSummaryLength) ?? string.Empty;
        Visibility = visibility;
        IsOpen = isOpen;
        RequiresApproval = requiresApproval;
        Revision++;
        ModifiedAt = now;
    }

    /// <summary>
    /// Replaces the tier list wholesale.
    /// <para>
    /// <b>Wholesale rather than per-tier, and the trade is deliberate.</b> The editor is a form that
    /// posts the whole set, and three separate add/update/remove routes would let a client apply two
    /// of the three and leave the programme in a shape nobody designed. What it costs is that a tier
    /// removed from the payload is removed from the programme — so the handler refuses to remove a
    /// tier that has members, which is where that rule belongs rather than here.
    /// </para>
    /// <para>
    /// <b>Filled counts survive.</b> A tier that appears in both the old and the new list keeps its
    /// counter: it is the same tier, and re-posting the form must not reset how many people are in
    /// it.
    /// </para>
    /// </summary>
    public Result ReplaceTiers(IReadOnlyList<MembershipTier> tiers, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        if (tiers.Count > CommunityProgrammeLimits.MaxTiers)
        {
            return Result.Failure(CommunityProgrammeErrors.Validation(
                $"A programme may have at most {CommunityProgrammeLimits.MaxTiers} tiers."));
        }

        var existing = _tiers.ToDictionary(tier => tier.Id);

        foreach (var tier in tiers)
        {
            if (existing.TryGetValue(tier.Id, out var previous))
            {
                tier.CarryFilledCount(previous.FilledCount);
            }
        }

        _tiers.Clear();
        _tiers.AddRange(tiers);

        Revision++;
        ModifiedAt = now;

        return Result.Success();
    }

    public MembershipTier? FindTier(MembershipTierId tierId) =>
        _tiers.FirstOrDefault(tier => tier.Id == tierId);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Counters
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sets the counters from a recount.
    /// <para>
    /// <b>Deliberately does not bump the revision.</b> A sweep reconciling counters while an
    /// administrator edits the tier list must not make the administrator's save fail — the same rule
    /// B12 applies to attendance settling under a role change.
    /// </para>
    /// </summary>
    public void Reconcile(
        int memberCount, IReadOnlyDictionary<MembershipTierId, int> filledByTier, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(filledByTier);

        MemberCount = Math.Max(0, memberCount);

        foreach (var tier in _tiers)
        {
            tier.SetFilledCount(filledByTier.TryGetValue(tier.Id, out var filled) ? filled : 0);
        }

        ModifiedAt = now;
    }

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    internal static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
