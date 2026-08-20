using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultCurrentUser : ICurrentUser
{
    public UserId? UserId => null;
    public string? Role => null;
    public bool IsAuthenticated => false;
    public IReadOnlyList<string> Permissions => [];
    public IReadOnlyDictionary<string, string> Claims => new Dictionary<string, string>();
}
