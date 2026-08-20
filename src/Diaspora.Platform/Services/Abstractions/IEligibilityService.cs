using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

public interface IEligibilityService
{
    Task<EligibilityResult> CheckEligibilityAsync(string action, CancellationToken cancellationToken = default);
}
