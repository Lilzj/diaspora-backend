using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Creators;

/// <summary>
/// One member's creator record — the projection the register says does not exist.
/// <para>
/// <b>The status on this row is the platform's answer and the only one.</b> The register: "nothing
/// says 'this person is an Established Creator' — and §7 forbids the UI inventing that rule from a
/// point count." It is persisted rather than computed on every read for two reasons that are both
/// about correctness rather than speed: a computed status has no previous value, so
/// <c>creator.status.changed</c> could not be published on a change rather than on every read; and
/// the directory and the leaderboard filter on it, which a computed value cannot be indexed on.
/// </para>
/// <para>
/// <b><see cref="EarnedStatus"/> and <see cref="Status"/> are two different facts.</b> The earned
/// rung is what the member's work bought them. The served status is that rung unless the platform
/// is saying something else about the account — pending a review, restricted, suspended — in which
/// case the override wins. Keeping them apart is what makes a lifted suspension restore the rung
/// instead of starting the climb again, and it is why <c>CreatorStatusPolicy</c> resolves the
/// overrides first.
/// </para>
/// <para>
/// <b>The counters are maintained from published events and never recomputed on a read.</b> Each is
/// an idempotent consumer's work; <see cref="RecognisedActions"/> and
/// <see cref="WeightedContributionScore"/> are the creator programme's own recognition of what
/// somebody did, not a measurement of what happened to it. There is no view, impression or reach
/// figure on this type — those are CREATOR-GAP-007's and belong to B19.
/// </para>
/// <para>
/// <b>Not <c>ISoftDeletable</c>, deliberately.</b> A creator record carries the history behind a
/// claimed milestone, a granted reward and an awarded grant. Leaving the programme is a
/// discoverability flag and a status; it is those precisely so the record survives.
/// </para>
/// </summary>
public sealed class CreatorProfile : AggregateRoot<CreatorId>, IAuditableEntity
{
    private readonly List<CreatorTag> _tags = [];

    private CreatorProfile(
        CreatorId id,
        Guid userId,
        string displayName,
        string? countryCode,
        DateTimeOffset joinedAt) : base(id)
    {
        UserId = userId;
        DisplayName = displayName;
        CountryCode = countryCode;
        JoinedAt = joinedAt;
        CreatedAt = joinedAt;
        UpdatedAt = joinedAt;
        StatusChangedAt = joinedAt;
    }

    private CreatorProfile() { }

    /// <summary>The platform user this creator record belongs to. Unique.</summary>
    public Guid UserId { get; private set; }

    // ── Presentation, denormalised from Identity ────────────────────────────────────────────
    //
    // Copied from UserRegisteredIntegrationEvent rather than read from Identity, because this
    // module takes no reference on Identity and a directory that joined to it on every row would
    // be a cross-module query in the query plan. Refreshed by the same consumer when Identity says
    // it changed.

    public string DisplayName { get; private set; } = string.Empty;

    public string? Handle { get; private set; }

    public string? AvatarUrl { get; private set; }

    public string? CoverImageUrl { get; private set; }

    public string? Bio { get; private set; }

    public string? CountryCode { get; private set; }

    // ── Programme standing ──────────────────────────────────────────────────────────────────

    /// <summary>What the platform says this member is. The served answer. See the class summary.</summary>
    public CreatorStatus Status { get; private set; } = CreatorStatus.NotACreator;

    /// <summary>The rung their work earned, ignoring any override. See the class summary.</summary>
    public CreatorStatus EarnedStatus { get; private set; } = CreatorStatus.NotACreator;

    public DateTimeOffset StatusChangedAt { get; private set; }

    /// <summary>Whether the creator-programme verification is currently approved.</summary>
    public bool IsVerifiedCreator { get; private set; }

    /// <summary>Whether a creator-programme application is with a reviewer right now.</summary>
    public bool HasVerificationInReview { get; private set; }

    /// <summary>
    /// Limited by the platform. A restricted creator reads everything and changes nothing the
    /// platform prohibits — which is enforced on the write rather than by hiding the button.
    /// </summary>
    public bool IsRestricted { get; private set; }

    public bool IsSuspended { get; private set; }

    /// <summary>
    /// Why, in words a member may be shown. Never names a risk check — see
    /// <c>CreatorErrors.SignupBonusNotEligible</c> for the same rule stated where it bites hardest.
    /// </summary>
    public string? StandingReason { get; private set; }

    // ── Discovery ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this creator may appear in the public directory and on public leaderboards.
    /// <para>
    /// <b>Enforced in the query, never by the client.</b> The register: "A client-side privacy
    /// filter is one refactor away from a privacy leak." The directory's predicate reads this
    /// column, and so does the facet count behind it.
    /// </para>
    /// </summary>
    public bool IsDiscoverable { get; private set; } = true;

    // ── Counters ────────────────────────────────────────────────────────────────────────────

    public long Followers { get; private set; }

    public long Following { get; private set; }

    /// <summary>Everything the creator has published that the programme recognises.</summary>
    public long TotalContentPublished { get; private set; }

    public long TotalHelpfulVotes { get; private set; }

    /// <summary>How many recognised actions this creator has to their name, unweighted.</summary>
    public long RecognisedActions { get; private set; }

    /// <summary>
    /// The weighted total of those actions. The lifetime leaderboard's score, and the input to the
    /// status ladder.
    /// </summary>
    public long WeightedContributionScore { get; private set; }

    public IReadOnlyList<CreatorTag> Tags => _tags.AsReadOnly();

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Opens a creator record.
    /// <para>
    /// Called from the Identity registration consumer, so every member has one from the day they
    /// join — which is what makes <see cref="CreatorStatus.NotACreator"/> a state rather than a
    /// missing row, and what lets the sign-up bonus have somewhere to live.
    /// </para>
    /// </summary>
    public static CreatorProfile Open(
        CreatorId id,
        Guid userId,
        string displayName,
        string? countryCode,
        DateTimeOffset joinedAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        return new CreatorProfile(
            id,
            userId,
            string.IsNullOrWhiteSpace(displayName) ? "Member" : displayName.Trim(),
            Normalise(countryCode)?.ToUpperInvariant(),
            joinedAt);
    }

    /// <summary>Refreshes the fields Identity owns. Never touches anything creator-specific.</summary>
    public void RefreshIdentity(
        string? displayName, string? countryCode, string? handle, string? avatarUrl, DateTimeOffset now)
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(displayName) && displayName.Trim() != DisplayName)
        {
            DisplayName = displayName.Trim();
            changed = true;
        }

        if (Normalise(countryCode)?.ToUpperInvariant() is { } country && country != CountryCode)
        {
            CountryCode = country;
            changed = true;
        }

        if (Normalise(handle) is { } normalisedHandle && normalisedHandle != Handle)
        {
            Handle = normalisedHandle;
            changed = true;
        }

        if (Normalise(avatarUrl) is { } normalisedAvatar && normalisedAvatar != AvatarUrl)
        {
            AvatarUrl = normalisedAvatar;
            changed = true;
        }

        if (changed)
        {
            Touch(now);
        }
    }

    /// <summary>
    /// Replaces the member's photo with what Identity now shows, including nothing.
    /// <para>
    /// Separate from <see cref="RefreshIdentity"/>, which never clears a field: a registration
    /// replayed with no photo must not wipe one, but a member who removed their photo has.
    /// </para>
    /// </summary>
    public void ChangeAvatar(string? avatarUrl, DateTimeOffset now)
    {
        var normalised = Normalise(avatarUrl);

        if (normalised == AvatarUrl)
        {
            return;
        }

        AvatarUrl = normalised;
        Touch(now);
    }

    /// <summary>
    /// Sets or clears the cover image, by the public address Media serves it at.
    /// </summary>
    public void ChangeCoverImage(string? coverImageUrl, DateTimeOffset now)
    {
        var normalised = Normalise(coverImageUrl);

        if (normalised == CoverImageUrl)
        {
            return;
        }

        CoverImageUrl = normalised;
        Touch(now);
    }

    /// <summary>
    /// The member's own edit.
    /// <para>
    /// <b>Every collection is replaced rather than merged, and each is bounded and de-duplicated
    /// here.</b> A caller sending forty categories gets the first eight; a caller sending the same
    /// one twice gets it once. Doing it in the aggregate rather than in a validator is what makes
    /// the invariant true of the record instead of true of one request.
    /// </para>
    /// </summary>
    public void UpdateProfile(
        string? bio,
        IEnumerable<string>? categories,
        IEnumerable<string>? expertise,
        IEnumerable<string>? languages,
        string? coverImageUrl,
        bool? isDiscoverable,
        DateTimeOffset now)
    {
        if (bio is not null)
        {
            var trimmed = bio.Trim();

            Bio = trimmed.Length == 0
                ? null
                : trimmed[..Math.Min(trimmed.Length, CreatorLimits.MaxBioLength)];
        }

        if (coverImageUrl is not null)
        {
            CoverImageUrl = Normalise(coverImageUrl);
        }

        if (categories is not null)
        {
            Replace(CreatorTagKind.Category, categories, CreatorLimits.MaxCategories);
        }

        if (expertise is not null)
        {
            Replace(CreatorTagKind.Expertise, expertise, CreatorLimits.MaxExpertise);
        }

        if (languages is not null)
        {
            Replace(CreatorTagKind.Language, languages, CreatorLimits.MaxLanguages);
        }

        if (isDiscoverable is { } discoverable)
        {
            IsDiscoverable = discoverable;
        }

        Touch(now);
    }

    /// <summary>
    /// Records a recognised action.
    /// <para>
    /// The counters this bumps are the status ladder's and the lifetime leaderboard's inputs. The
    /// entry itself lives in the activity ledger; this is the running total, kept on the row so a
    /// directory page does not aggregate a million entries to sort twenty creators.
    /// </para>
    /// </summary>
    public void RecordActivity(string actionType, int weight, DateTimeOffset now)
    {
        RecognisedActions++;
        WeightedContributionScore += Math.Max(0, weight);

        if (CreatorActivityTypes.Publications.Contains(actionType, StringComparer.Ordinal))
        {
            TotalContentPublished++;
        }

        Touch(now);
    }

    /// <summary>Follower and helpful-vote counters, from B6's published contracts.</summary>
    public void AdjustAudience(long followerDelta, long followingDelta, long helpfulDelta, DateTimeOffset now)
    {
        Followers = Math.Max(0, Followers + followerDelta);
        Following = Math.Max(0, Following + followingDelta);
        TotalHelpfulVotes = Math.Max(0, TotalHelpfulVotes + helpfulDelta);
        Touch(now);
    }

    /// <summary>
    /// Applies a status the policy decided.
    /// <para>
    /// Returns true when the served status actually moved, which is the only condition on which
    /// <c>creator.status.changed</c> is published. §23 of the phase brief: "Do not publish events
    /// merely because an endpoint exists."
    /// </para>
    /// </summary>
    public bool ApplyStatus(CreatorStatus earned, CreatorStatus served, DateTimeOffset now)
    {
        var previous = Status;

        EarnedStatus = earned;

        if (served == previous)
        {
            // The earned rung may still have moved underneath an override. Persist it and say
            // nothing, because nothing the member can see has changed.
            Touch(now);
            return false;
        }

        Status = served;
        StatusChangedAt = now;
        Touch(now);

        RaiseDomainEvent(new CreatorStatusChangedDomainEvent(Id, UserId, previous, served, now));

        return true;
    }

    /// <summary>What the creator-programme application currently says about this member.</summary>
    public void ApplyVerificationStanding(bool isVerified, bool isInReview, DateTimeOffset now)
    {
        IsVerifiedCreator = isVerified;
        HasVerificationInReview = isInReview;
        Touch(now);
    }

    /// <summary>
    /// The platform limiting or suspending a creator.
    /// <para>
    /// Separate from the status ladder on purpose: this sets the fact, and the policy decides what
    /// the member is then called. A handler that set <see cref="CreatorStatus.Suspended"/> directly
    /// would be a second place the ladder is decided.
    /// </para>
    /// </summary>
    public void ApplyStanding(bool isRestricted, bool isSuspended, string? reason, DateTimeOffset now)
    {
        IsRestricted = isRestricted;
        IsSuspended = isSuspended;
        StandingReason = Normalise(reason);
        Touch(now);
    }

    /// <summary>The tags of one kind, in the order the member gave them.</summary>
    public IReadOnlyList<string> TagValues(CreatorTagKind kind) =>
        _tags
            .Where(tag => tag.Kind == kind)
            .OrderBy(tag => tag.SortOrder)
            .Select(tag => tag.Value)
            .ToList();

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

    private void Replace(CreatorTagKind kind, IEnumerable<string> values, int limit)
    {
        _tags.RemoveAll(tag => tag.Kind == kind);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = 0;

        foreach (var value in values)
        {
            if (order >= limit)
            {
                break;
            }

            var normalised = Normalise(value);

            if (normalised is null || normalised.Length > CreatorLimits.MaxTagLength)
            {
                continue;
            }

            if (!seen.Add(normalised))
            {
                continue;
            }

            _tags.Add(CreatorTag.Create(kind, normalised, order));
            order++;
        }
    }

    private static string? Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        UpdatedAt = now;
        ModifiedAt = now;
    }
}

/// <summary>Which list a tag belongs to.</summary>
public enum CreatorTagKind
{
    Category = 0,
    Expertise = 1,
    Language = 2
}

/// <summary>
/// One category, area of expertise or language on a creator record.
/// <para>
/// <b>One owned table for three lists rather than three tables, and the directory is why.</b> A
/// creator search filters on any of them; one table with a <see cref="Kind"/> column gives that
/// query one index to use rather than three joins. The composite key is (creator, kind, value),
/// which is also the uniqueness rule — a creator cannot hold the same category twice, and the
/// database rather than the aggregate is the last word on that.
/// </para>
/// <para>
/// <b><see cref="Value"/> is stored as the member wrote it and matched case-insensitively.</b> A
/// facet that showed "Housing" and "housing" as two different filters would be a facet nobody
/// trusts.
/// </para>
/// </summary>
public sealed class CreatorTag : ValueObject
{
    private CreatorTag(CreatorTagKind kind, string value, int sortOrder)
    {
        Kind = kind;
        Value = value;
        NormalisedValue = value.ToUpperInvariant();
        SortOrder = sortOrder;
    }

    private CreatorTag() { }

    public CreatorTagKind Kind { get; private set; }

    /// <summary>As the member wrote it. What the profile renders.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Upper-cased, indexed, and what every filter and facet compares against.</summary>
    public string NormalisedValue { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static CreatorTag Create(CreatorTagKind kind, string value, int sortOrder) =>
        new(kind, value.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return NormalisedValue;
    }
}
