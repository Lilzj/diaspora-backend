using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One member confirming that another member's contribution is accurate.
/// <para>
/// The frontend calls this "community voting" (<c>POST /trust/community-voting</c>), which
/// oversells it — nothing is tallied toward a majority and no outcome turns on the count. It
/// is a single peer confirmation that feeds the recipient's trust dimension, which is why the
/// backend names it a confirmation.
/// </para>
/// <para>
/// Stored as a row per (confirmer, target) rather than a counter, because the invariant that
/// makes the signal worth anything — one confirmation per member per item, and never your own
/// — cannot be enforced against a counter.
/// </para>
/// </summary>
public sealed class CommunityContribution : AggregateRoot<CommunityContributionId>, IAuditableEntity
{
    public const int MaxTargetTypeLength = 64;
    public const int MaxTargetIdLength = 128;
    public const int MaxNoteLength = 500;

    /// <summary>Who gave the confirmation.</summary>
    public UserId ConfirmedByUserId { get; private set; } = default!;

    /// <summary>Who wrote the thing being confirmed, and therefore earns the points.</summary>
    public UserId AuthorUserId { get; private set; } = default!;

    /// <summary>What kind of thing — "community-update", "guide".</summary>
    public string TargetType { get; private set; } = default!;

    public string TargetId { get; private set; } = default!;

    /// <summary>Which country community this counted toward.</summary>
    public string? CountryCode { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private CommunityContribution() { }

    private CommunityContribution(
        CommunityContributionId id,
        UserId confirmedByUserId,
        UserId authorUserId,
        string targetType,
        string targetId,
        DateTimeOffset confirmedAt) : base(id)
    {
        ConfirmedByUserId = confirmedByUserId;
        AuthorUserId = authorUserId;
        TargetType = targetType;
        TargetId = targetId;
        ConfirmedAt = confirmedAt;
    }

    /// <summary>
    /// Records a confirmation.
    /// <para>
    /// Confirming your own contribution is refused here rather than in the handler — it is
    /// the rule that stops the trust dimension being self-service, and it belongs with the
    /// thing it protects.
    /// </para>
    /// </summary>
    public static CommunityContribution Record(
        UserId confirmedByUserId,
        UserId authorUserId,
        string targetType,
        string targetId,
        DateTimeOffset confirmedAt,
        string? countryCode = null,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(confirmedByUserId);
        ArgumentNullException.ThrowIfNull(authorUserId);

        if (confirmedByUserId == authorUserId)
        {
            throw new DomainException(
                "A member cannot confirm their own contribution.",
                "trust.confirmation.self-forbidden");
        }

        var type = Truncate(
            Guard.Against.NullOrWhiteSpace(targetType, nameof(targetType)), MaxTargetTypeLength)!
            .ToLowerInvariant();

        var target = Truncate(
            Guard.Against.NullOrWhiteSpace(targetId, nameof(targetId)), MaxTargetIdLength)!;

        return new CommunityContribution(
            CommunityContributionId.New(), confirmedByUserId, authorUserId, type, target, confirmedAt)
        {
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            Note = Truncate(note, MaxNoteLength)
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

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
}
