using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.SharedKernel.Contracts;

namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Who a campaign is aimed at — MON-GAP-007's request DTO.
/// <para>
/// <b>Five dimensions, and every one of them is honoured.</b> The register's complaint was that the
/// builder collected seven and the wire carried one, so a field here that the delivery engine
/// ignored would be the same defect in a new place. Age, gender and inferred ethnicity are absent
/// deliberately: §55 forbids inferring identity characteristics.
/// </para>
/// <para>
/// Every list is optional and an empty one means "do not narrow on this", independently per
/// dimension.
/// </para>
/// </summary>
public sealed record AdAudienceRequest
{
    /// <summary>ISO 3166-1 alpha-2. Case is normalised server-side.</summary>
    public IReadOnlyList<string>? CountryCodes { get; init; }

    public IReadOnlyList<Guid>? CommunityIds { get; init; }

    public IReadOnlyList<Guid>? NeighbourhoodIds { get; init; }

    public IReadOnlyList<string>? Interests { get; init; }

    public IReadOnlyList<string>? Categories { get; init; }
}

/// <summary>
/// Drafts a campaign. Nothing is charged and nothing is booked.
/// </summary>
public sealed record CreateAdCampaignRequest
{
    public string? Name { get; init; }

    /// <summary><c>Awareness</c>, <c>Traffic</c>, <c>Engagement</c> or <c>Leads</c>.</summary>
    /// <remarks>
    /// A string rather than the enum on purpose: the handler parses case-insensitively and
    /// answers <c>ObjectiveUnknown</c>, which is a better failure than the model binder's 400.
    /// The attribute is what puts the four legal values in the published contract, where a
    /// client can read them — without it the document says only "string", which is how six
    /// objectives shipped against four.
    /// </remarks>
    [OpenApiEnumValues(typeof(AdCampaignObjective))]
    public string? Objective { get; init; }

    public long BudgetMinorUnits { get; init; }

    public string? Currency { get; init; }

    /// <summary>Defaults to the currency's own exponent when absent.</summary>
    public int? Exponent { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Required. An advertising campaign may not run open-ended.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    public AdAudienceRequest? Audience { get; init; }

    public Guid? CreativeMediaId { get; init; }

    public string? Headline { get; init; }

    /// <summary>What the advertisement's button says. Clipped to 24 characters.</summary>
    public string? CallToActionLabel { get; init; }

    public string? Body { get; init; }

    /// <summary>What a click leads to — the subject the campaign promotes.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    /// <summary>The catalogue product being bought, which is what prices it.</summary>
    public string? ProductCode { get; init; }
}

/// <summary>
/// Rewrites a draft.
/// <para>
/// An alias rather than a derived record, because <see cref="CreateAdCampaignRequest"/> is sealed
/// and unsealing it to be inherited once would be a worse trade. The shape is deliberately
/// identical: a partial edit is a shape the client would have to hold twice and the server would
/// have to merge, and merging is where an unset field becomes an erased one.
/// </para>
/// </summary>
public sealed record UpdateAdCampaignRequest
{
    public string? Name { get; init; }

    /// <remarks>
    /// A string rather than the enum on purpose: the handler parses case-insensitively and
    /// answers <c>ObjectiveUnknown</c>, which is a better failure than the model binder's 400.
    /// The attribute is what puts the four legal values in the published contract, where a
    /// client can read them — without it the document says only "string", which is how six
    /// objectives shipped against four.
    /// </remarks>
    [OpenApiEnumValues(typeof(AdCampaignObjective))]
    public string? Objective { get; init; }

    public long BudgetMinorUnits { get; init; }

    public string? Currency { get; init; }

    public int? Exponent { get; init; }

    public DateTimeOffset? StartsAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    public AdAudienceRequest? Audience { get; init; }

    public Guid? CreativeMediaId { get; init; }

    public string? Headline { get; init; }

    /// <summary>What the advertisement's button says. Clipped to 24 characters.</summary>
    public string? CallToActionLabel { get; init; }

    public string? Body { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? ProductCode { get; init; }

    /// <summary>The same fields, as the shape the factory reads.</summary>
    public CreateAdCampaignRequest AsDraft() => new()
    {
        Name = Name,
        Objective = Objective,
        BudgetMinorUnits = BudgetMinorUnits,
        Currency = Currency,
        Exponent = Exponent,
        StartsAt = StartsAt,
        EndsAt = EndsAt,
        Audience = Audience,
        CreativeMediaId = CreativeMediaId,
        Headline = Headline,
        CallToActionLabel = CallToActionLabel,
        Body = Body,
        SubjectType = SubjectType,
        SubjectId = SubjectId,
        ProductCode = ProductCode
    };
}

/// <summary>Stops a campaign for good. The reason is shown back to the advertiser.</summary>
public sealed record CancelAdCampaignRequest
{
    public string? Reason { get; init; }
}

/// <summary>A reviewer's decision. The note is required on a rejection and shown verbatim.</summary>
public sealed record DecideAdCampaignRequest
{
    /// <summary><c>approve</c> or <c>reject</c>.</summary>
    public string? Decision { get; init; }

    public string? Note { get; init; }
}

/// <summary>The advertiser contesting a rejection. One appeal, not a thread.</summary>
public sealed record AppealAdCampaignRequest
{
    public string? Note { get; init; }
}

/// <summary>
/// A click on a promoted subject.
/// <para>
/// Carries the surface so an advertiser can see where their campaign is working, and nothing else:
/// the platform already knows the campaign from the path and deliberately does not want to know who
/// clicked.
/// </para>
/// </summary>
public sealed record RecordAdClickRequest
{
    public string? Surface { get; init; }
}

/// <summary>
/// Submitting a campaign, which opens the purchase that funds it.
/// <para>
/// The idempotency key matters here more than anywhere else on this surface: submission opens a
/// charge, and a double tap on a slow connection must not open two.
/// </para>
/// </summary>
public sealed record SubmitAdCampaignRequest
{
    public string? MethodKind { get; init; }

    public string? IdempotencyKey { get; init; }
}
