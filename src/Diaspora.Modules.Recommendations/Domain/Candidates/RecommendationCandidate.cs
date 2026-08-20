using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Candidates;

/// <summary>
/// A shadow of something that exists somewhere else.
/// <para>
/// <b>This is the one place the "owns no business data" claim needs stating carefully, so it is
/// stated here.</b> A recommendation engine that knows nothing about the things it recommends can
/// only return identifiers in a random order. To rank a job against a flat it must know that both
/// exist, roughly where, roughly what about, and roughly how popular — and there is exactly one
/// legitimate way to know that without reaching into eleven modules: project it from the contracts
/// those modules already publish.
/// </para>
/// <para>
/// <b>So this is a projection, and the distinction from ownership is real rather than
/// semantic.</b> Every field here arrives from a published integration event and none of it is
/// authoritative: the Jobs module can change a title, close a posting or delete it, and this row is
/// wrong until the next event arrives. Nothing in the platform reads a candidate row to answer a
/// question about a job — the API returns the identifier and the client asks Jobs. Truncate this
/// table and the platform loses nothing but ranking quality until the outbox is replayed. That is
/// the test, and it is the test an architecture rule asserts: no write path outside this module's
/// consumers may create one.
/// </para>
/// <para>
/// <b>The title and summary are carried anyway, and that is a deliberate exception with a
/// reason.</b> A feed that returned forty identifiers would force the client into forty round trips
/// across eleven modules to render one screen, on a mobile connection, in a country where that
/// connection is expensive. The copy here is a cache for rendering, marked as such in the API
/// response, and every card links to the owning module's own endpoint for anything that matters.
/// </para>
/// <para>
/// <b>Soft-deletable, unlike the signal.</b> A candidate is withdrawn when its source is unpublished
/// — a job closes, a flat is taken — and "was recommendable until Tuesday" is a fact the analytics
/// rollups need to explain a drop in acceptance. The retention sweep hard-deletes them eventually.
/// </para>
/// </summary>
public sealed class RecommendationCandidate
    : AggregateRoot<RecommendationCandidateId>, IAuditableEntity, ISoftDeletable
{
    /// <summary>How many attributes one candidate may carry. Beyond this it is a search index.</summary>
    public const int MaximumAttributes = 60;

    /// <summary>
    /// The ceiling on a paid placement's weight.
    /// <para>
    /// One, deliberately — the same scale every ranking score on this platform is normalised to. A
    /// placement priced above it is clamped rather than rejected, because refusing a weight would put
    /// this module in the position of vetoing a purchase B15 has already taken money for.
    /// </para>
    /// </summary>
    public const decimal MaximumPromotionWeight = 1m;

    private readonly List<CandidateAttribute> _attributes = [];

    private RecommendationCandidate(
        RecommendationCandidateId id,
        TargetReference target,
        string sourceModule,
        DateTimeOffset createdAt) : base(id)
    {
        Target = target;
        SourceModule = sourceModule;
        CreatedAt = createdAt;
        IsActive = true;
        Visibility = CandidateVisibility.Members;
    }

    private RecommendationCandidate() { }

    /// <summary>What this is a shadow of. Two fields, and the module's whole knowledge of it.</summary>
    public TargetReference Target { get; private set; } = default!;

    /// <summary>Which module published the contract this was projected from.</summary>
    public string SourceModule { get; private set; } = string.Empty;

    /// <summary>A rendering cache, never a source of truth. See the class summary.</summary>
    public string Title { get; private set; } = string.Empty;

    public string? Summary { get; private set; }

    /// <summary>The owning module's deep-link handle, when it publishes one.</summary>
    public string? Slug { get; private set; }

    /// <summary>An opaque Media reference. Never a URL — B5's rule, and this module honours it.</summary>
    public string? MediaReference { get; private set; }

    public string? CategoryKey { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string? CountryCode { get; private set; }

    public string? RegionName { get; private set; }

    public string? CityName { get; private set; }

    public string? NeighborhoodName { get; private set; }

    /// <summary>BCP 47, lower case. Null when the source publishes nothing.</summary>
    public string? LanguageCode { get; private set; }

    /// <summary>
    /// Who wrote or owns it, when the contract names them.
    /// <para>
    /// Held for exactly two purposes — never recommending a member their own listing, and honouring
    /// a block on an author — and both are policies rather than strategies.
    /// </para>
    /// </summary>
    public UserId? OwnerUserId { get; private set; }

    /// <summary>Whether the owning module said this carries a Trust mark. Never decided here.</summary>
    public bool IsVerified { get; private set; }

    /// <summary>Whether the owning module said this is featured. A ranking input, not a guarantee.</summary>
    public bool IsFeatured { get; private set; }

    /// <summary>
    /// What a paid placement over this subject is worth, as the Financial Platform priced it. Zero
    /// when nothing is paid for.
    /// <para>
    /// <b>Deliberately not <see cref="IsFeatured"/>, and the separation is the whole point.</b> B15
    /// draws the line explicitly: <c>isFeatured</c> is display state, set free by a moderator, and
    /// stays with the module that renders it; a placement is financial state — what was bought, under
    /// which policy version, for how long, by whom, for how much. Keeping them in two fields is what
    /// makes <em>"did this rank because it is good or because it was paid for"</em> answerable, and
    /// merging them would make it permanently unanswerable.
    /// </para>
    /// <para>
    /// Set only by a consumer of B15's placement contracts. Nothing else may write it, and this
    /// module never decides it — B15 owns the price and the weight, and B14 owns what the weight does
    /// to an ordering.
    /// </para>
    /// </summary>
    public decimal PromotionWeight { get; private set; }

    /// <summary>
    /// When the paid placement stops, or null for one with no end.
    /// <para>
    /// <b>Held rather than inferred, because a placement outlives its own end event.</b> B15's sweep
    /// ends lapsed placements every ten minutes, so between a placement's last second and the sweep
    /// that notices, this row still says it is promoted. Ranking compares this against the request's
    /// own clock, which closes that window without either module polling the other.
    /// </para>
    /// </summary>
    public DateTimeOffset? PromotedUntil { get; private set; }

    /// <summary>0–1, normalised by the projecting consumer against its own vertical.</summary>
    public decimal PopularityScore { get; private set; }

    /// <summary>0–1. How much the platform can vouch for it.</summary>
    public decimal QualityScore { get; private set; }

    /// <summary>Views, applications, RSVPs — whatever the vertical counts. Ranked relatively, never absolutely.</summary>
    public long EngagementCount { get; private set; }

    public CandidateVisibility Visibility { get; private set; }

    /// <summary>The lowest tier that may be shown this. Projected from the source's own gate.</summary>
    public int MinimumTrustLevel { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>When the source last changed. Drives the recency strategy for evergreen content.</summary>
    public DateTimeOffset? SourceUpdatedAt { get; private set; }

    /// <summary>When it stops being recommendable — a closing date, an event's start.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>False the moment the source unpublishes. The availability policy's input.</summary>
    public bool IsActive { get; private set; }

    public IReadOnlyList<CandidateAttribute> Attributes => _attributes.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public static RecommendationCandidate Project(
        RecommendationCandidateId id,
        TargetReference target,
        string sourceModule,
        string title,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceModule);

        return new RecommendationCandidate(id, target, sourceModule.Trim(), createdAt)
        {
            Title = Truncate(title)
        };
    }

    /// <summary>
    /// Re-projects everything a later event carried.
    /// <para>
    /// <b>Every parameter is optional and null means "unchanged", which is the only sound reading
    /// for a projection.</b> Contracts are not all the same shape: a <c>ListingUpdated</c> event
    /// carries a title and no location, and treating its silence about location as "the listing has
    /// no location" would blank the field every time somebody fixed a typo.
    /// </para>
    /// </summary>
    public void Describe(
        string? title = null,
        string? summary = null,
        string? slug = null,
        string? mediaReference = null,
        string? categoryKey = null,
        string? languageCode = null)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = Truncate(title);
        if (!string.IsNullOrWhiteSpace(summary)) Summary = Truncate(summary, 1024);
        if (!string.IsNullOrWhiteSpace(slug)) Slug = slug.Trim();
        if (!string.IsNullOrWhiteSpace(mediaReference)) MediaReference = mediaReference.Trim();
        if (!string.IsNullOrWhiteSpace(categoryKey)) CategoryKey = categoryKey.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(languageCode)) LanguageCode = languageCode.Trim().ToLowerInvariant();
    }

    public void Place(
        string? countryCode = null,
        string? regionName = null,
        string? cityName = null,
        string? neighborhoodName = null)
    {
        if (!string.IsNullOrWhiteSpace(countryCode)) CountryCode = countryCode.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(regionName)) RegionName = regionName.Trim();
        if (!string.IsNullOrWhiteSpace(cityName)) CityName = cityName.Trim();
        if (!string.IsNullOrWhiteSpace(neighborhoodName)) NeighborhoodName = neighborhoodName.Trim();
    }

    public void Attribute(UserId? ownerUserId, bool? isVerified = null, bool? isFeatured = null)
    {
        if (ownerUserId is not null) OwnerUserId = ownerUserId;
        if (isVerified is not null) IsVerified = isVerified.Value;
        if (isFeatured is not null) IsFeatured = isFeatured.Value;
    }

    public void Rate(decimal? popularityScore = null, decimal? qualityScore = null, long? engagementCount = null)
    {
        if (popularityScore is not null) PopularityScore = Math.Clamp(popularityScore.Value, 0m, 1m);
        if (qualityScore is not null) QualityScore = Math.Clamp(qualityScore.Value, 0m, 1m);
        if (engagementCount is not null) EngagementCount = Math.Max(0, engagementCount.Value);
    }

    public void Restrict(CandidateVisibility visibility, int minimumTrustLevel = 0)
    {
        Visibility = visibility;
        MinimumTrustLevel = Math.Max(0, minimumTrustLevel);
    }

    public void Schedule(
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? sourceUpdatedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        if (publishedAt is not null) PublishedAt = publishedAt;
        if (sourceUpdatedAt is not null) SourceUpdatedAt = sourceUpdatedAt;
        if (expiresAt is not null) ExpiresAt = expiresAt;
    }

    /// <summary>Replaces the attribute set outright. A re-projection, not a merge.</summary>
    public void SetAttributes(IEnumerable<CandidateAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        _attributes.Clear();

        foreach (var attribute in attributes.Take(MaximumAttributes))
        {
            _attributes.Add(attribute);
        }
    }

    /// <summary>
    /// Records a paid placement the Financial Platform activated over this subject.
    /// <para>
    /// <b>The weight is clamped and never trusted raw.</b> B15 prices a placement and this module
    /// decides what a price does to an ordering; an unclamped weight arriving from a mispriced
    /// product would let one purchase outrank every organic result on the platform for as long as the
    /// placement ran. The ceiling means a boost competes with the strongest editorial signal rather
    /// than erasing it.
    /// </para>
    /// <para>
    /// <b>Last activation wins, rather than accumulating.</b> Two overlapping placements over one
    /// subject are two purchases, not a doubled weight — B15 already models that as one placement per
    /// subject-and-kind, and summing here would make a member who bought twice by accident outrank a
    /// member who bought once by design.
    /// </para>
    /// </summary>
    public void Promote(decimal weight, DateTimeOffset? until = null)
    {
        PromotionWeight = Math.Clamp(weight, 0m, MaximumPromotionWeight);
        PromotedUntil = until;
    }

    /// <summary>
    /// The placement finished — lapsed, cancelled or refunded, all three of which end it.
    /// <para>
    /// Clears the window as well as the weight. A cleared weight with a live window left behind would
    /// read, to anything inspecting the row, as a promotion that was running and worth nothing.
    /// </para>
    /// </summary>
    public void EndPromotion()
    {
        PromotionWeight = 0m;
        PromotedUntil = null;
    }

    /// <summary>
    /// Whether a paid placement is running at the given moment.
    /// <para>
    /// Takes the clock rather than reading it, because ranking evaluates a page of candidates against
    /// one instant and a candidate that expired between the first and the four-hundredth would
    /// otherwise be judged against a different moment from its neighbours.
    /// </para>
    /// </summary>
    public bool IsPromotedAt(DateTimeOffset moment) =>
        PromotionWeight > 0m && (PromotedUntil is null || PromotedUntil > moment);

    /// <summary>The source unpublished it. Stays in the table; stops being recommendable.</summary>
    public void Withdraw() => IsActive = false;

    /// <summary>The source published it again.</summary>
    public void Reinstate() => IsActive = true;

    /// <summary>
    /// Whether this may be ranked at all, right now.
    /// <para>
    /// Read by the candidate source rather than by a policy, because it is a fact about the
    /// candidate rather than a decision about a member — and a policy that had to check it would
    /// have to run before every other policy to be correct.
    /// </para>
    /// </summary>
    public bool IsRecommendable(DateTimeOffset asOf) =>
        IsActive && !IsDeleted && (ExpiresAt is null || ExpiresAt > asOf);

    public bool HasAttribute(InterestKind kind, string key) =>
        _attributes.Exists(attribute =>
            attribute.Kind == kind &&
            string.Equals(attribute.Key, key.Trim().ToLowerInvariant(), StringComparison.Ordinal));

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

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        IsActive = false;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    private static string Truncate(string? value, int length = 256)
    {
        var trimmed = (value ?? string.Empty).Trim();

        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}
