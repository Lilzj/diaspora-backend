using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Authorization;

/// <summary>
/// The composed, platform-wide feature-gate catalogue.
/// </summary>
public interface IFeatureCatalog
{
    /// <summary>Every declared gate, ordered by module then name.</summary>
    IReadOnlyList<FeatureDefinition> All { get; }

    /// <summary>The full open/closed picture for a subject, with reasons for the closed ones.</summary>
    FeatureEvaluation Evaluate(AuthorizationSubject subject);

    /// <summary>Whether one named gate is open. Cheaper than evaluating the whole set.</summary>
    bool IsOpen(string featureName, AuthorizationSubject subject);

    /// <summary>Looks up a declaration by name; <c>null</c> when nothing declares it.</summary>
    FeatureDefinition? Find(string featureName);
}
