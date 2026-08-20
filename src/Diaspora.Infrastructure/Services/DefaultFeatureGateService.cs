using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultFeatureGateService : IFeatureGateService
{
    public Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<bool> IsEnabledAsync(string featureName, FeatureGateContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
