namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// The weight behind a recommendation.
/// <para>
/// Three kinds because the brief names reviews, recommendations and testimonials as distinct
/// contracts and they carry different amounts of the recommender's own reputation. A
/// <see cref="Recommend"/> is a tap; a <see cref="Testimonial"/> is a quotable paragraph the
/// provider may display on its profile; an <see cref="Endorsement"/> is a professional peer
/// vouching, and is the one B15 and B17 should weight most heavily.
/// </para>
/// <para>
/// Distinct from a review, which carries a star rating and rolls into
/// <c>ProviderReviewSummary</c>. A recommendation has no rating — it is unambiguously positive,
/// which is why it can be withdrawn but never scored.
/// </para>
/// </summary>
public enum RecommendationKind
{
    /// <summary>"I recommend this provider." No text required.</summary>
    Recommend = 1,

    /// <summary>A written account the provider may display. Text required.</summary>
    Testimonial = 2,

    /// <summary>A professional peer vouching for competence in a named area.</summary>
    Endorsement = 3
}
