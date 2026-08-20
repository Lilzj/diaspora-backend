using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.UnitTests.Common;

public sealed class FakeCurrentUser : ICurrentUser
{
    public UserId? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public IReadOnlyDictionary<string, string> Claims { get; set; } = new Dictionary<string, string>();

    public static FakeCurrentUser Anonymous() => new();

    public static FakeCurrentUser Authenticated(UserId? userId = null, string role = "CommunityUser") => new()
    {
        UserId = userId ?? Diaspora.SharedKernel.Identity.UserId.New(),
        Role = role,
        IsAuthenticated = true
    };

    public static FakeCurrentUser Admin(UserId? userId = null) =>
        Authenticated(userId, "Admin");

    public static FakeCurrentUser Provider(UserId? userId = null) =>
        Authenticated(userId, "ServiceProvider");
}
