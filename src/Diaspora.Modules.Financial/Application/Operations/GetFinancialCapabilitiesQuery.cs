using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// What this deployment's Financial Platform can actually do.
/// <para>
/// Modelled on Media's capabilities endpoint, for the same reason: nobody should have to infer from
/// behaviour whether a seam has an adapter behind it.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetFinancialCapabilitiesQuery : IQuery<FinancialCapabilitiesResponse>;
