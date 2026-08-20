using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// A pointer at somebody else's data. A type and an identifier, and nothing more.
/// <para>
/// <b>This is the entire shape of what this module knows about a job, a flat or a guide, and its
/// smallness is the point.</b> Every claim B14 makes about owning no business data reduces to the
/// fact that a reference is two fields: the module that owns the thing can rename it, reprice it,
/// republish it or delete it, and nothing here needs to change or even to notice.
/// </para>
/// <para>
/// <b>The identifier is a <see cref="Guid"/> rather than a strongly typed one, and that is
/// deliberate.</b> A <c>JobId</c> lives in the Jobs module; taking a dependency on it to hold a
/// reference would be the exact coupling this module refuses. The pair is the identity — a job and a
/// housing listing may in principle share a <see cref="Guid"/>, and every index in this schema is
/// declared on both columns for that reason.
/// </para>
/// <para>
/// <b>No static <c>Empty</c> or <c>None</c> instance is declared, and that is not an oversight.</b>
/// A cached singleton on an owned value object is shared by every aggregate that touches it, and EF
/// writes nulls into its columns the moment two aggregates hold the same instance. The platform has
/// paid for that lesson twice.
/// </para>
/// </summary>
public sealed class TargetReference : ValueObject
{
    private TargetReference(RecommendationTargetType targetType, Guid targetId)
    {
        TargetType = targetType;
        TargetId = targetId;
    }

    private TargetReference() { }

    public RecommendationTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public static TargetReference Create(RecommendationTargetType targetType, Guid targetId)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException(
                "A target reference needs an identifier. An empty one would match every row in the "
                + "candidate table for its type.",
                nameof(targetId));
        }

        return new TargetReference(targetType, targetId);
    }

    /// <summary>The stable string form used as a dictionary key and a suppression key.</summary>
    public string Key => $"{TargetType}:{TargetId:N}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TargetType;
        yield return TargetId;
    }
}
