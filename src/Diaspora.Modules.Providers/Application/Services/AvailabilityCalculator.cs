using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Availability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Answers "is this provider open?" for one provider or a whole page.
/// <para>
/// Delegates to the registry so a booking module can override the answer, and falls back to the
/// weekly schedule. The value it adds over calling the registry directly is
/// <see cref="EvaluateManyAsync"/>: a discovery page asks the question twenty times against one
/// instant, and evaluating each row against its own <c>UtcNow</c> would let a page straddle a
/// closing time and show two providers as open and closed for the same minute.
/// </para>
/// </summary>
public sealed class AvailabilityCalculator
{
    private readonly AvailabilityProviderRegistry _registry;
    private readonly IClock _clock;

    public AvailabilityCalculator(AvailabilityProviderRegistry registry, IClock clock)
    {
        _registry = registry;
        _clock = clock;
    }

    public async Task<AvailabilityAnswer> EvaluateAsync(
        Provider provider,
        UserId? viewerId = null,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return await _registry.EvaluateAsync(
            new AvailabilityQuery
            {
                Provider = provider,
                At = at ?? _clock.UtcNow,
                ViewerId = viewerId
            },
            cancellationToken);
    }

    /// <summary>
    /// Answers for a page, against one instant.
    /// <para>
    /// Sequential rather than concurrent, deliberately. A contributed provider will usually hold
    /// a scoped repository or a <c>DbContext</c>, and EF Core's context is explicitly not
    /// thread-safe — fanning out here would produce the intermittent "a second operation was
    /// started on this context" that only ever reproduces under load.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, AvailabilityAnswer>> EvaluateManyAsync(
        IReadOnlyList<Provider> providers,
        UserId? viewerId = null,
        DateTimeOffset? at = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var instant = at ?? _clock.UtcNow;
        var answers = new Dictionary<Guid, AvailabilityAnswer>(providers.Count);

        foreach (var provider in providers)
        {
            answers[provider.Id.Value] =
                await EvaluateAsync(provider, viewerId, instant, cancellationToken);
        }

        return answers;
    }
}
