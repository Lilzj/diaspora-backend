using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Integrations.Application.Services;

/// <summary>
/// Who is asking, resolved once.
/// <para>
/// <b>The subject is the token's, never a parameter.</b> The register says so for every route in
/// this module — "the subject is the token's, never a parameter" — and there is exactly one way to
/// obtain it here. No handler takes an owner identifier from a request, no route has an owner path
/// segment, and there is no read-another-member's-connections variant to write by accident.
/// </para>
/// <para>
/// <b>Only <see cref="IntegrationOwnerKind.User"/> is reachable.</b> The schema supports community,
/// organization and platform ownership because §11 requires ownership to be explicit and a bare
/// user column cannot express it, and no route creates one. When a later phase adds a
/// community-owned connection it adds a resolver here and a role check beside it; until then, every
/// query filters on a pair whose first half is a constant, which is what makes the isolation test
/// meaningful rather than tautological.
/// </para>
/// </summary>
public readonly record struct IntegrationCaller(IntegrationOwnerKind OwnerKind, Guid OwnerId)
{
    /// <summary>The signed-in member, or the reason there is not one.</summary>
    public static Result<IntegrationCaller> From(Guid? userId) =>
        userId is { } id && id != Guid.Empty
            ? Result.Success(new IntegrationCaller(IntegrationOwnerKind.User, id))
            : Result.Failure<IntegrationCaller>(IntegrationErrors.NotAuthenticated());
}
