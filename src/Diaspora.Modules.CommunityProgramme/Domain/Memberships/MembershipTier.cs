using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityProgramme.Domain.Memberships;

/// <summary>
/// One rung of a membership programme: a price, what it gets you, and what it asks of you.
/// <para>
/// An owned entity of <see cref="MembershipProgramme"/>. It has an identifier because the client
/// puts one in a URL and an application names one, and it is still owned because a tier outside its
/// programme has no meaning.
/// </para>
/// <para>
/// <b><see cref="IsFull"/> is computed here and served as a field.</b> The register is explicit that
/// the client never compares <see cref="FilledCount"/> to <see cref="Capacity"/>, and this is why:
/// an uncapped tier is never full, and a client comparing a number to a null would have to encode
/// that rule itself in every screen that draws a join button.
/// </para>
/// </summary>
public sealed class MembershipTier
{
    private readonly List<TierBenefit> _benefits = [];
    private readonly List<TierRequirement> _requirements = [];

    private MembershipTier(
        MembershipTierId id,
        string name,
        string description,
        ProgrammePrice? price,
        BillingPeriod billingPeriod,
        int? capacity,
        bool isRecommended,
        int sortOrder)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        BillingPeriod = billingPeriod;
        Capacity = capacity;
        IsRecommended = isRecommended;
        SortOrder = sortOrder;
    }

    private MembershipTier() { }

    public MembershipTierId Id { get; private set; } = default!;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    /// <summary>Null for a free tier. Never a zero — the absence is the meaning.</summary>
    public ProgrammePrice? Price { get; private set; }

    public BillingPeriod BillingPeriod { get; private set; }

    /// <summary>Null where the tier is uncapped, which is the usual case.</summary>
    public int? Capacity { get; private set; }

    /// <summary>How many memberships currently hold this tier. Reconciled, never accumulated.</summary>
    public int FilledCount { get; private set; }

    public bool IsRecommended { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyList<TierBenefit> Benefits => _benefits.AsReadOnly();

    public IReadOnlyList<TierRequirement> Requirements =>
        _requirements.OrderBy(requirement => requirement.SortOrder).ToList().AsReadOnly();

    /// <summary>Server-decided. An uncapped tier is never full.</summary>
    public bool IsFull => Capacity is { } capacity && FilledCount >= capacity;

    /// <summary>How many months a membership at this tier runs before it needs renewing.</summary>
    public int? RenewalMonths => BillingPeriod switch
    {
        BillingPeriod.Monthly => 1,
        BillingPeriod.Quarterly => 3,
        BillingPeriod.Yearly => 12,

        // A one-off membership and a lifetime one both never renew. They are different words for
        // different products — one is a single event's worth of belonging, the other is for ever —
        // and both answer null to "when does this lapse".
        _ => null
    };

    public static MembershipTier Create(
        MembershipTierId id,
        string name,
        string? description,
        ProgrammePrice? price,
        BillingPeriod billingPeriod,
        int? capacity,
        bool isRecommended,
        int sortOrder,
        IEnumerable<TierBenefit>? benefits = null,
        IEnumerable<TierRequirement>? requirements = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var tier = new MembershipTier(
            id,
            MembershipProgramme.Clip(name, CommunityProgrammeLimits.MaxNameLength) ?? string.Empty,
            MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxDescriptionLength)
                ?? string.Empty,
            price,
            billingPeriod,
            capacity is > 0 ? capacity : null,
            isRecommended,
            sortOrder);

        if (benefits is not null)
        {
            tier._benefits.AddRange(benefits.Take(CommunityProgrammeLimits.MaxBenefitsPerTier));
        }

        if (requirements is not null)
        {
            tier._requirements.AddRange(
                requirements.Take(CommunityProgrammeLimits.MaxRequirementsPerTier));
        }

        return tier;
    }

    /// <summary>Keeps the counter across a wholesale tier replacement. See <c>ReplaceTiers</c>.</summary>
    internal void CarryFilledCount(int filledCount) => FilledCount = Math.Max(0, filledCount);

    internal void SetFilledCount(int filledCount) => FilledCount = Math.Max(0, filledCount);
}

/// <summary>
/// Something a tier gets you.
/// <para>
/// <b>The icon is the server's choice.</b> The catalogue owns its own iconography, so a deployment
/// adding a benefit adds its icon with it rather than shipping a client release to render it.
/// </para>
/// </summary>
public sealed class TierBenefit
{
    private TierBenefit(Guid id, string title, string? description, string? icon, bool isActive)
    {
        Id = id;
        Title = title;
        Description = description;
        Icon = icon;
        IsActive = isActive;
    }

    private TierBenefit() { }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>An icon name the client renders. Never parsed by anything here.</summary>
    public string? Icon { get; private set; }

    /// <summary>False where a benefit is advertised and not yet live.</summary>
    public bool IsActive { get; private set; }

    public static TierBenefit Create(
        Guid id, string title, string? description, string? icon, bool isActive) =>
        new(
            id,
            MembershipProgramme.Clip(title, CommunityProgrammeLimits.MaxLabelLength) ?? string.Empty,
            MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxDescriptionLength),
            MembershipProgramme.Clip(icon, CommunityProgrammeLimits.MaxIconLength),
            isActive);
}

/// <summary>
/// Something a tier asks of an applicant before they may join.
/// <para>
/// <b>Whether it is met is not stored here, and that is the whole design.</b> "Is this person
/// verified" is a fact about the caller at the moment of asking, not a property of the tier, and
/// storing it would mean a requirement that was met last month still reading as met today. The
/// evaluation happens on every read, server-side, and the client renders the answer — §19 forbids it
/// computing one.
/// </para>
/// </summary>
public sealed class TierRequirement
{
    private TierRequirement(
        Guid id,
        string label,
        string? description,
        MembershipRequirementKind kind,
        string? actionRoute,
        int? minimumAge,
        int sortOrder)
    {
        Id = id;
        Label = label;
        Description = description;
        Kind = kind;
        ActionRoute = actionRoute;
        MinimumAge = minimumAge;
        SortOrder = sortOrder;
    }

    private TierRequirement() { }

    public Guid Id { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public MembershipRequirementKind Kind { get; private set; }

    /// <summary>Where to send somebody who does not meet it yet.</summary>
    public string? ActionRoute { get; private set; }

    /// <summary>Set on an <see cref="MembershipRequirementKind.Age"/> requirement.</summary>
    public int? MinimumAge { get; private set; }

    public int SortOrder { get; private set; }

    public static TierRequirement Create(
        Guid id,
        string label,
        string? description,
        MembershipRequirementKind kind,
        string? actionRoute,
        int? minimumAge,
        int sortOrder) =>
        new(
            id,
            MembershipProgramme.Clip(label, CommunityProgrammeLimits.MaxLabelLength) ?? string.Empty,
            MembershipProgramme.Clip(description, CommunityProgrammeLimits.MaxDescriptionLength),
            kind,
            MembershipProgramme.Clip(actionRoute, CommunityProgrammeLimits.MaxRouteLength),
            minimumAge is > 0 ? minimumAge : null,
            sortOrder);
}
