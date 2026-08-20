using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

public interface IFeatureGateService
{
    Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default);
    Task<bool> IsEnabledAsync(string featureName, FeatureGateContext context, CancellationToken cancellationToken = default);
}
