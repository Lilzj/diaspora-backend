using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Entitlements;

/// <summary>
/// Everything the caller is entitled to, and how much of it they have used.
/// <para>
/// <b>The route that makes a client-side entitlement decision unnecessary.</b> The client's gate
/// decides what to draw; this is what decides what is true, and every route that gates on an
/// entitlement re-reads it rather than trusting what a client was told.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.SubscriptionsRead)]
public sealed record GetMyEntitlementsQuery : IQuery<EntitlementSnapshotResponse>;

/// <summary>Resolves the caller's entitlements and the consumption verticals reported.</summary>
public sealed class GetMyEntitlementsQueryHandler
    : IQueryHandler<GetMyEntitlementsQuery, EntitlementSnapshotResponse>
{
    private readonly EntitlementResolver _resolver;
    private readonly FinancialAuthorizationService _authorization;

    public GetMyEntitlementsQueryHandler(
        EntitlementResolver resolver, FinancialAuthorizationService authorization)
    {
        _resolver = resolver;
        _authorization = authorization;
    }

    public async Task<Result<EntitlementSnapshotResponse>> Handle(
        GetMyEntitlementsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        return caller.IsFailure
            ? Result.Failure<EntitlementSnapshotResponse>(caller.Error)
            : Result.Success(await _resolver.ResolveAsync(caller.Value, cancellationToken));
    }
}
