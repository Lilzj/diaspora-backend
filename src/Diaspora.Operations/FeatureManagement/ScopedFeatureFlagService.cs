using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.FeatureManagement;

/// <summary>
/// The per-request face of feature flags: the singleton's state, this request's caller.
/// <para>
/// <b>This type exists because a singleton cannot know who is calling, and the container proves it.</b>
/// An earlier draft injected <c>ICurrentUser</c> — which is scoped, because it reads the current
/// request's claims — straight into <see cref="FeatureFlagService"/>, which is a singleton because it
/// holds the flag set, the kill switch and the change history. The container's scope validation
/// refused to build the graph, and it was right to: a captured scoped service is resolved once, at
/// startup, so every flag evaluation for the life of the process would have been made against
/// whichever caller happened to be first — or against no caller at all.
/// </para>
/// <para>
/// The split is the textbook fix and it costs one small class. State lives in the singleton;
/// <b>identity lives here</b>, where it is resolved per request like everything else that depends on
/// who is asking.
/// </para>
/// </summary>
public sealed class ScopedFeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagService _flags;
    private readonly ICurrentUser? _currentUser;

    public ScopedFeatureFlagService(FeatureFlagService flags, ICurrentUser? currentUser = null)
    {
        ArgumentNullException.ThrowIfNull(flags);

        _flags = flags;
        _currentUser = currentUser;
    }

    public ValueTask<bool> IsEnabledAsync(string flag, CancellationToken cancellationToken = default) =>
        _flags.IsEnabledAsync(flag, CurrentContext(), cancellationToken);

    public ValueTask<bool> IsEnabledAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default) =>
        _flags.IsEnabledAsync(flag, context, cancellationToken);

    public ValueTask<FeatureFlagEvaluation> EvaluateAsync(
        string flag, FeatureFlagContext context, CancellationToken cancellationToken = default) =>
        _flags.EvaluateAsync(flag, context, cancellationToken);

    public Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default) =>
        _flags.ListAsync(cancellationToken);

    /// <summary>
    /// The evaluation context for the current request.
    /// <para>
    /// Country is deliberately absent. The kernel's <c>ICurrentUser</c> does not carry one — a
    /// member's country lives in their profile, which is Identity's data rather than a claim — so a
    /// country rollout needs the caller to supply the context explicitly. Guessing it here from an
    /// address or a header would give a rollout an input that is wrong for anyone travelling, and it
    /// would be wrong silently.
    /// </para>
    /// </summary>
    private FeatureFlagContext CurrentContext() => new(
        _currentUser?.UserId?.ToString(),
        TenantId: null,
        CountryCode: null,
        Roles: _currentUser?.Role is { } role ? [role] : null,
        Environment: _flags.EnvironmentName);
}
