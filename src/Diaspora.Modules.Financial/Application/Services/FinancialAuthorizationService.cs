using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Answers "who is asking", once.
/// <para>
/// Every member-facing financial handler needs the caller's identifier and none of them should
/// reconstruct the failure. A handler that read <c>ICurrentUser</c> directly would have to decide
/// what to do when it is absent, and eleven handlers deciding that separately is eleven chances to
/// return a 500 where a 401 was meant.
/// </para>
/// </summary>
public sealed class FinancialAuthorizationService
{
    private readonly ICurrentUser _currentUser;

    public FinancialAuthorizationService(ICurrentUser currentUser) => _currentUser = currentUser;

    /// <summary>The caller, or a failure that maps to 401.</summary>
    public Result<Guid> RequireUserId()
    {
        var userId = _currentUser.UserId?.Value;

        return userId is null || userId == Guid.Empty
            ? Result.Failure<Guid>(Error.Unauthorized(
                FinancialErrorCodes.NotAuthenticated, "Sign in to use this."))
            : Result.Success(userId.Value);
    }

    /// <summary>The caller, or null. For paths where anonymity is legitimate.</summary>
    public Guid? UserIdOrNull() =>
        _currentUser.UserId?.Value is { } id && id != Guid.Empty ? id : null;

    /// <summary>Whether the caller holds a permission.</summary>
    public bool Has(string permission) =>
        _currentUser.Permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>A claim the caller's token carries, or null.</summary>
    public string? Claim(string name) =>
        _currentUser.Claims.TryGetValue(name, out var value) ? value : null;
}
