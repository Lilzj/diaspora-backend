using Diaspora.SharedKernel.Identity;

namespace Diaspora.Platform.Services.Abstractions;

public interface ICurrentUser
{
    UserId? UserId { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyDictionary<string, string> Claims { get; }
}
