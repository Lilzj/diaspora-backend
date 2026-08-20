using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Events;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.Reviews;

/// <summary>
/// One member vouching for a provider.
/// <para>
/// Distinct from a <see cref="ProviderReview"/> in the one way that matters: it has no rating.
/// A recommendation is unambiguously positive, which is why it can be withdrawn but never
/// scored, and why it does not touch <c>ProviderReviewSummary</c>. A member who wants to say
/// "four out of five, the food is good but the wait is long" writes a review; a member who wants
/// to say "go here" recommends.
/// </para>
/// <para>
/// The brief asks for reusable contracts for reviews, recommendations, ratings and provider
/// reputation while explicitly forbidding "complex recommendation algorithms". This aggregate is
/// the data half of that: it records who vouched for whom, in what capacity, and why. What B15
/// and B17 do with it is theirs.
/// </para>
/// <para>
/// <see cref="RecommendationKind.Endorsement"/> is the one worth weighting differently
/// downstream — a solicitor endorsing an accountant carries information a customer's
/// recommendation does not — which is why the kind is stored rather than flattened away.
/// </para>
/// </summary>
public sealed class ProviderRecommendation
    : AggregateRoot<ProviderRecommendationId>, IAuditableEntity, ISoftDeletable
{
    public const int MaxSkillLength = 120;

    private ProviderRecommendation(
        ProviderRecommendationId id,
        ProviderId providerId,
        UserId recommenderId,
        RecommendationKind kind,
        string? note,
        DateTimeOffset createdAt)
        : base(id)
    {
        ProviderId = providerId;
        RecommenderId = recommenderId;
        Kind = kind;
        Note = note;
        CreatedAt = createdAt;
    }

    private ProviderRecommendation() { }

    public ProviderId ProviderId { get; private set; } = default!;

    public UserId RecommenderId { get; private set; } = default!;

    public RecommendationKind Kind { get; private set; }

    /// <summary>Required for a testimonial, optional otherwise.</summary>
    public string? Note { get; private set; }

    /// <summary>What is being endorsed. "Immigration appeals", "West African catering".</summary>
    public string? Skill { get; private set; }

    /// <summary>
    /// Whether the provider has agreed to show this on its profile. Testimonials are quoted
    /// publicly, and quoting somebody without asking is the kind of thing that ends up in a
    /// complaint rather than in a marketing page.
    /// </summary>
    public bool IsFeatured { get; private set; }

    /// <summary>Withdrawn by its author. Kept rather than deleted so a count cannot drift.</summary>
    public bool IsWithdrawn { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Whether this recommendation counts and may be shown.</summary>
    public bool IsActive => !IsDeleted && !IsWithdrawn;

    public static Result<ProviderRecommendation> Create(
        ProviderRecommendationId id,
        ProviderId providerId,
        UserId recommenderId,
        RecommendationKind kind,
        DateTimeOffset now,
        string? note = null,
        string? skill = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(recommenderId);

        var trimmedNote = note?.Trim();

        if (trimmedNote is { Length: > ProviderLimits.MaxRecommendationNoteLength })
        {
            return Result.Failure<ProviderRecommendation>(ProviderErrors.RecommendationNoteTooLong);
        }

        // A testimonial with no text is a recommendation wearing the wrong label, and a provider
        // that displayed it would show an empty quotation mark on its profile.
        if (kind == RecommendationKind.Testimonial && string.IsNullOrEmpty(trimmedNote))
        {
            return Result.Failure<ProviderRecommendation>(ProviderErrors.RecommendationNoteTooLong);
        }

        var recommendation = new ProviderRecommendation(
            id, providerId, recommenderId, kind, trimmedNote, now)
        {
            Skill = Trim(skill, MaxSkillLength)
        };

        recommendation.RaiseDomainEvent(new ProviderRecommendationReceivedDomainEvent
        {
            RecommendationId = id,
            ProviderId = providerId,
            RecommenderId = recommenderId,
            Kind = kind
        });

        return Result.Success(recommendation);
    }

    public Result Revise(string? note, string? skill)
    {
        if (!IsActive)
        {
            return Result.Failure(ProviderErrors.RecommendationNotFound);
        }

        var trimmed = note?.Trim();

        if (trimmed is { Length: > ProviderLimits.MaxRecommendationNoteLength })
        {
            return Result.Failure(ProviderErrors.RecommendationNoteTooLong);
        }

        if (Kind == RecommendationKind.Testimonial && string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(ProviderErrors.RecommendationNoteTooLong);
        }

        Note = trimmed;
        Skill = Trim(skill, MaxSkillLength);

        return Result.Success();
    }

    public void Withdraw()
    {
        IsWithdrawn = true;
        IsFeatured = false;
    }

    public void Reinstate() => IsWithdrawn = false;

    /// <summary>The provider chooses to display this on its profile.</summary>
    public void SetFeatured(bool isFeatured) => IsFeatured = isFeatured && IsActive;

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        IsFeatured = false;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
