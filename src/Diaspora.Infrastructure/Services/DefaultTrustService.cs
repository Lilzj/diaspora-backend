using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Services.Models;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultTrustService : ITrustService
{
    public Task<TrustLevel> GetTrustLevelAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TrustLevel.Basic);

    public Task<bool> IsPublishingEligibleAsync(UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
