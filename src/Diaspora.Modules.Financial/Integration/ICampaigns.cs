using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Integration;

/// <summary>
/// B15's fundraising campaign operations, for the module that owns the URL they are served under.
/// <para>
/// <b>Thirteen methods because B15 has thirteen campaign operations, not because a seam should be
/// this wide.</b> The width is a consequence of a rule somewhere else: this platform allows exactly
/// one module per URL prefix — <c>The_module_maps_one_route_prefix</c> enforces it, and B15's is
/// <c>/api/v1/financial</c> — while the register puts the campaign surface under
/// <c>/api/v1/communities</c>. So B18 maps the routes and B15 makes the decisions, and every
/// operation on the page has to cross the boundary.
/// </para>
/// <para>
/// <b>B15 had already published every request and response these carry.</b> All thirteen are
/// <c>Financial.Contracts.V1</c> types; what was missing was anywhere to put the operation, so B18
/// constructed B15's <c>Application.Campaigns</c> commands itself and dispatched them. That is the
/// same shape B5 was in before <c>IMediaAttachments</c>: <b>the payload published, the operation
/// not</b>, and B15 likewise had no <c>Integration</c> namespace at all until this seam.
/// </para>
/// <para>
/// <b>What was considered instead.</b> B15 could publish the route mapping — a
/// <c>Map(RouteGroupBuilder)</c> B18 calls with its own group — which would move roughly three
/// hundred lines of B15's HTTP surface back to B15 and leave B18 naming one thing. It does not
/// break the prefix rule, because a mapping helper is not an <c>IEndpointContributor</c>. It was
/// not taken: B18's <c>CampaignEndpoints</c> documents the present split as deliberate — <i>the
/// route is ours and the decision is theirs</i> — and reversing a stated architectural decision is
/// not something an encapsulation fix should do on its way past. This seam closes the finding and
/// leaves that decision where its author left it.
/// </para>
/// </summary>
public interface ICampaigns
{
    /// <summary>Creates a campaign, in draft. Idempotent.</summary>
    Task<Result<CampaignResponse>> CreateAsync(
        CreateCampaignRequest request, CancellationToken cancellationToken = default);

    /// <summary>Campaigns a member can browse. A draft is visible only to the person who wrote it.</summary>
    Task<Result<GivingPageResponse<CampaignResponse>>> SearchAsync(
        CampaignSearch search, CancellationToken cancellationToken = default);

    /// <summary>One campaign, with server-computed progress.</summary>
    Task<Result<CampaignResponse>> GetAsync(
        Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Edits a campaign's own text. Nothing financial moves through it.</summary>
    Task<Result<CampaignResponse>> UpdateAsync(
        Guid campaignId, UpdateCampaignRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a campaign to <paramref name="target"/>, or refuses when the state machine forbids it.
    /// <para>
    /// The destination is a string because the caller names it from the route it served, never from
    /// the body: a client cannot ask for a state the URL it called does not mean. B15 parses it and
    /// refuses an unknown one, so a caller cannot invent a state by spelling.
    /// </para>
    /// </summary>
    Task<Result<CampaignResponse>> TransitionAsync(
        Guid campaignId, string target, CampaignTransitionRequest? request,
        CancellationToken cancellationToken = default);

    /// <summary>A campaign's timeline, newest first. Cursor-paged.</summary>
    Task<Result<GivingPageResponse<CampaignUpdateResponse>>> GetUpdatesAsync(
        Guid campaignId, string? cursor, int? take,
        CancellationToken cancellationToken = default);

    /// <summary>Publishes an update to a campaign's supporters. Idempotent.</summary>
    Task<Result<CampaignUpdateResponse>> PublishUpdateAsync(
        Guid campaignId, CreateCampaignUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>A campaign's milestones, each with the server's own reached stamp.</summary>
    Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> GetMilestonesAsync(
        Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Declares a campaign's milestones. Reached-ness is never the caller's.</summary>
    Task<Result<IReadOnlyList<CampaignMilestoneResponse>>> SetMilestonesAsync(
        Guid campaignId, SetCampaignMilestonesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>A campaign's supporters, anonymised at the source. Cursor-paged.</summary>
    Task<Result<GivingPageResponse<CampaignSupporterResponse>>> GetSupportersAsync(
        Guid campaignId, string? cursor, int? take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's own donations to one campaign, newest first. Anonymous donations are absent:
    /// they store no donor.
    /// </summary>
    Task<Result<IReadOnlyList<CampaignDonationResponse>>> GetMyDonationsAsync(
        Guid campaignId, int? take, CancellationToken cancellationToken = default);

    /// <summary>Raised, allocated, disbursed and remaining, each labelled with its authority.</summary>
    Task<Result<CampaignFundUsageResponse>> GetFundUsageAsync(
        Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Declares how a campaign's money is allocated. A spend is marked verified only where a
    /// settled payout backs it.
    /// </summary>
    Task<Result<CampaignFundUsageResponse>> SetFundUsageAsync(
        Guid campaignId, SetCampaignFundUsageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What narrows a campaign search.
/// <para>
/// A record rather than seven parameters, because the only caller builds it from seven optional
/// query-string values and a positional signature would let two of the three strings swap without
/// a compiler error.
/// </para>
/// </summary>
public sealed record CampaignSearch
{
    /// <summary>A campaign state by name. Unknown names narrow to nothing rather than failing.</summary>
    public string? State { get; init; }

    public string? Category { get; init; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Only the caller's own campaigns, drafts included. The one flag that widens.</summary>
    public bool MineOnly { get; init; }

    public string? Cursor { get; init; }

    public int? Take { get; init; }
}
