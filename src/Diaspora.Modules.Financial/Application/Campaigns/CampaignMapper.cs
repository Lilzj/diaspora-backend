using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Application.Campaigns;

/// <summary>
/// Turns a campaign and its cause into the client's shape.
/// <para>
/// <b>The percentage is computed here and nowhere else.</b> The register is explicit that there is
/// no arithmetic on the device dividing a raised by a target, and the only way that stays true is if
/// the server always sends the answer. Rounded down, so a campaign at 99.6% does not render as
/// complete.
/// </para>
/// <para>
/// <b>Every figure comes from the cause, never from the campaign.</b> A campaign that kept its own
/// copy of "raised" would be a second number for the same money, and the two would disagree the
/// first time a donation was refunded. Before publication there is no cause, and the response says
/// zero raised — which is true, because nothing has been given.
/// </para>
/// </summary>
internal static class CampaignMapper
{
    /// <param name="canDecideReview">
    /// Whether this caller may publish or reject. Filters the transitions offered, so the response
    /// never names a move the handler would refuse. See <c>CampaignAccessService.CanDecideReview</c>.
    /// </param>
    public static CampaignResponse ToResponse(
        Campaign campaign,
        DonationCause? cause,
        bool canManage,
        DateTimeOffset now,
        string? symbol,
        bool canDecideReview = true)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var raised = cause?.RaisedMinorUnits ?? 0L;

        return new CampaignResponse
        {
            CampaignId = campaign.Id.Value,
            Title = campaign.Title,
            Summary = campaign.Summary,
            Story = campaign.Story,
            CoverImage = campaign.CoverImageReference,
            Gallery = campaign.GalleryReferences,
            Category = CategoryName(campaign.Category),
            Status = campaign.Status.ToString(),
            Progress = new CampaignProgressResponse
            {
                Target = campaign.Target is { } target
                    ? FinancialMapper.ToResponse(target, symbol)
                    : null,
                Raised = FinancialMapper.ToResponse(
                    Money.FromMinorUnits(raised, campaign.Currency, campaign.Exponent), symbol),
                Percentage = Percentage(raised, campaign.TargetMinorUnits),
                SupporterCount = cause?.SupporterCount ?? 0,
                Deadline = campaign.Deadline,
                DaysRemaining = DaysRemaining(campaign.Deadline, now)
            },
            CauseCode = campaign.CauseCode,
            BeneficiaryType = campaign.BeneficiaryType.ToString(),
            BeneficiaryId = campaign.BeneficiaryId,
            BeneficiaryName = campaign.BeneficiaryName,
            Urgency = campaign.Urgency.ToString(),
            AcceptedKinds = [.. campaign.AcceptedKindList.Select(kind => kind.ToString())],
            OrganizerId = campaign.OrganizerUserId,
            OrganizerName = campaign.OrganizerName,
            OrganizationId = campaign.OrganizationId,
            IsVerified = cause?.IsVerified ?? false,
            CountryCode = campaign.CountryCode,
            LocationLabel = campaign.LocationLabel,
            CreatedAt = campaign.CreatedAt,
            PublishedAt = campaign.PublishedAt,
            ClosedAt = campaign.ClosedAt,
            ReviewNote = campaign.ReviewNote,
            CanManage = canManage,
            CanDonate = campaign.AcceptsDonations,
            // Publication and rejection are the review decision, and are listed only for a caller
            // who may actually make it. Naming a move the handler refuses is how a client ends up
            // rendering a button that answers 403. Suspending and restoring are moderation's and
            // are never listed: no organiser route makes them.
            AvailableTransitions = canManage
                ? [.. CampaignTransitions.From(campaign.Status)
                    .Where(status =>
                        canDecideReview
                        || status is not (CampaignStatus.Published or CampaignStatus.Rejected))
                    .Where(status => !CampaignTransitions.IsModerationMove(campaign.Status, status))
                    .Select(status => status.ToString())]
                : []
        };
    }

    public static CampaignUpdateResponse ToResponse(CampaignUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);

        return new CampaignUpdateResponse
        {
            UpdateId = update.Id.Value,
            CampaignId = update.CampaignId.Value,
            Title = update.Title,
            Body = update.Body,
            MediaReferences = update.Media,
            PublishedAt = update.PublishedAt,
            AuthorName = update.AuthorName,
            IsWithdrawn = update.IsWithdrawn
        };
    }

    public static CampaignMilestoneResponse ToResponse(
        CampaignMilestone milestone, string currency, int exponent, string? symbol)
    {
        ArgumentNullException.ThrowIfNull(milestone);

        return new CampaignMilestoneResponse
        {
            MilestoneId = milestone.Id,
            Label = milestone.Label,
            Target = FinancialMapper.ToResponse(
                Money.FromMinorUnits(milestone.TargetMinorUnits, currency, exponent), symbol),
            IsReached = milestone.IsReached,
            ReachedAt = milestone.ReachedAt
        };
    }

    /// <summary>
    /// Turns a donation into a supporter, applying anonymity at the source.
    /// <para>
    /// <b>An anonymous gift returns no identifier and no message.</b> Not a null beside a real one:
    /// the donation never stored a donor, and the message is dropped as well because free text
    /// somebody wrote is one of the commonest ways an identity travels. The display name is a
    /// placeholder the client renders as-is.
    /// </para>
    /// <para>
    /// <b>Neither is there a correlation handle.</b> No donation id, no receipt reference, no hash.
    /// Two anonymous gifts from one person are indistinguishable from two people, which is what
    /// anonymity means — and a stable per-donor token, however opaque, would let anybody with the
    /// list group them and count.
    /// </para>
    /// </summary>
    public static CampaignSupporterResponse ToSupporter(Donation donation, string? symbol)
    {
        ArgumentNullException.ThrowIfNull(donation);

        if (donation.IsAnonymous || donation.DonorUserId is null)
        {
            return new CampaignSupporterResponse
            {
                UserId = null,
                DisplayName = "Anonymous",
                IsAnonymous = true,
                Amount = FinancialMapper.ToResponse(donation.Amount, symbol),
                Message = null,
                GivenAt = donation.CompletedAt ?? donation.CreatedAt
            };
        }

        return new CampaignSupporterResponse
        {
            UserId = donation.DonorUserId,
            DisplayName = null,
            IsAnonymous = false,
            Amount = FinancialMapper.ToResponse(donation.Amount, symbol),
            Message = donation.Message,
            GivenAt = donation.CompletedAt ?? donation.CreatedAt
        };
    }

    /// <summary>
    /// 0–100, rounded down, or null where there is no target.
    /// <para>
    /// Null rather than zero: a percentage of nothing is undefined, and rendering a 0% bar for an
    /// untargeted campaign would state something false about a campaign that may have raised
    /// thousands.
    /// </para>
    /// </summary>
    public static int? Percentage(long raisedMinorUnits, long? targetMinorUnits)
    {
        if (targetMinorUnits is not { } target || target <= 0)
        {
            return null;
        }

        var percentage = (int)Math.Floor(raisedMinorUnits * 100d / target);

        return Math.Clamp(percentage, 0, 100);
    }

    /// <summary>
    /// Whole days left, or null where there is no deadline. Never negative.
    /// <para>
    /// Server-decided, because the clock on a phone is whatever its owner set it to — the same
    /// reason the state model gives for never deriving a status from a date on the device.
    /// </para>
    /// </summary>
    public static int? DaysRemaining(DateTimeOffset? deadline, DateTimeOffset now) =>
        deadline is { } ends
            ? Math.Max(0, (int)Math.Ceiling((ends - now).TotalDays))
            : null;

    /// <summary>
    /// The cause code a campaign publishes under.
    /// <para>
    /// <b>Derived from the campaign's identifier rather than from its title.</b> Two campaigns
    /// called "Help Ade get home" would collide, and a title is editable while a cause code is the
    /// key donations name for ever. A short prefix keeps it readable in a support conversation.
    /// </para>
    /// </summary>
    public static string CauseCodeFor(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return $"campaign-{campaign.Id.Value:n}"[..Math.Min(
            $"campaign-{campaign.Id.Value:n}".Length, 64)];
    }

    /// <summary>
    /// The campaign a cause code names, or null when it names something else.
    /// <para>
    /// <b>The companion to <see cref="CauseCodeFor"/>, and deliberately beside it.</b> A donation
    /// carries no campaign reference of its own — the cause code is the only tie back, so
    /// something has to read the encoding. Doing it here keeps both directions in one place; doing
    /// it at the point of need would put a second copy of the format in another module, which is
    /// how the two drift and a campaign's donations quietly stop being attributed to it.
    /// </para>
    /// <para>
    /// Anything that is not this format is a cause of another kind — a fund, a seeded appeal —
    /// and answers null rather than throwing.
    /// </para>
    /// </summary>
    public static Guid? CampaignIdFrom(string? causeCode)
    {
        const string Prefix = "campaign-";

        if (string.IsNullOrWhiteSpace(causeCode)
            || !causeCode.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParseExact(causeCode[Prefix.Length..], "N", out var id) ? id : null;
    }

    /// <summary>The currency symbol for a campaign, or null.</summary>
    public static async Task<string?> SymbolAsync(
        CurrencyCatalog currencies, string currency, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currencies);

        var all = await currencies.AllAsync(cancellationToken);

        return all.TryGetValue(currency, out var facts) ? facts.Symbol : null;
    }

    /// <summary>
    /// The category as the client spells it.
    /// <para>
    /// <b>Three of the eleven carry a space and a C# enum member cannot.</b> The client's union type
    /// is <c>'Community Project' | 'Legal Support' | 'Disaster Relief' | …</c> and it is the
    /// certified contract; returning <c>CommunityProject</c> would be a value no branch of the
    /// client matches, and it would fail silently as an unstyled chip rather than as an error. The
    /// three exceptions are listed rather than derived, because inserting a space before every
    /// capital would also turn a future <c>NGO</c> into <c>N G O</c>.
    /// </para>
    /// </summary>
    public static string CategoryName(CampaignCategory category) => category switch
    {
        CampaignCategory.CommunityProject => "Community Project",
        CampaignCategory.LegalSupport => "Legal Support",
        CampaignCategory.DisasterRelief => "Disaster Relief",
        _ => category.ToString()
    };

    /// <summary>Parses a category, defaulting to <see cref="CampaignCategory.Other"/>.</summary>
    public static CampaignCategory ParseCategory(string? value) =>
        Enum.TryParse<CampaignCategory>(Normalize(value), true, out var category)
            && Enum.IsDefined(category)
            ? category
            : CampaignCategory.Other;

    /// <summary>
    /// Parses a status, or null.
    /// <para>
    /// Null rather than a default, because a status filter nobody recognises must not silently
    /// become "Draft" and return somebody's unpublished campaigns.
    /// </para>
    /// </summary>
    public static CampaignStatus? ParseStatus(string? value) =>
        Enum.TryParse<CampaignStatus>(Normalize(value), true, out var status) && Enum.IsDefined(status)
            ? status
            : null;

    /// <summary>
    /// Strips the spaces the client's own category names carry.
    /// <para>
    /// The client sends <c>"Community Project"</c> and <c>"Legal Support"</c> — its own display
    /// strings — and the enum members cannot contain a space. Normalising here rather than asking
    /// the client to change is the smaller move, and the response sends the enum name back.
    /// </para>
    /// </summary>
    private static string Normalize(string? value) =>
        value?.Replace(" ", string.Empty, StringComparison.Ordinal).Trim() ?? string.Empty;
}
