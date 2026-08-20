using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultEligibilityService : IEligibilityService
{
    public Task<EligibilityResult> CheckEligibilityAsync(string action, CancellationToken cancellationToken = default) =>
        Task.FromResult(EligibilityResult.Eligible());
}
