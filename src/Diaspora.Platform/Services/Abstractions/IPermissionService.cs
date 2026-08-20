namespace Diaspora.Platform.Services.Abstractions;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<bool> AuthorizeAsync(string policy, CancellationToken cancellationToken = default);
}
