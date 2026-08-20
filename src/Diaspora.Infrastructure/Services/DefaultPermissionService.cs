using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultPermissionService : IPermissionService
{
    public Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<bool> AuthorizeAsync(string policy, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
