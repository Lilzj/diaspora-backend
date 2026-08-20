using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// One matchable fact about a candidate, in the same vocabulary a profile interest uses.
/// <para>
/// <b>Sharing <see cref="InterestKind"/> with the profile is the entire mechanism of interest
/// matching.</b> A job publishes tags and skills; a guide publishes a category and tags; a housing
/// listing publishes amenities. Projected into one shape, matching a member's interests against a
/// candidate becomes a set intersection rather than eleven per-vertical comparisons — which is what
/// lets the interest strategy be one class instead of one per module.
/// </para>
/// <para>
/// Everything is normalised to lower case on the way in, because a tag published as
/// <c>Visa-Sponsorship</c> by Jobs and one recorded as <c>visa-sponsorship</c> from a client signal
/// are the same interest, and a case-sensitive intersection would silently match neither.
/// </para>
/// </summary>
public sealed class CandidateAttribute : ValueObject
{
    private CandidateAttribute(Guid attributeId, InterestKind kind, string key)
    {
        AttributeId = attributeId;
        Kind = kind;
        Key = key;
    }

    private CandidateAttribute() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid AttributeId { get; private set; }

    public InterestKind Kind { get; private set; }

    public string Key { get; private set; } = string.Empty;

    public static CandidateAttribute Create(Guid attributeId, InterestKind kind, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new CandidateAttribute(attributeId, kind, key.Trim().ToLowerInvariant());
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AttributeId;
        yield return Kind;
        yield return Key;
    }
}
