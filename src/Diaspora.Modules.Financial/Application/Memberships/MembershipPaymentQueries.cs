using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Extensibility.Memberships;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Memberships;

/// <summary>
/// Every payment bound to one membership, for the reconciliation surface and for support.
/// <para>
/// Read-only and keyed by the membership. The caller must hold it, asked of the owning module
/// exactly as the write path does.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetMembershipPaymentsQuery
    : IQuery<IReadOnlyList<MembershipPaymentBindingResponse>>
{
    public required Guid MembershipId { get; init; }
}

public sealed class GetMembershipPaymentsQueryHandler
    : IQueryHandler<GetMembershipPaymentsQuery, IReadOnlyList<MembershipPaymentBindingResponse>>
{
    private readonly IMembershipPaymentBindingRepository _bindings;
    private readonly IPaymentIntentRepository _intents;
    private readonly IMembershipSubjectAuthority _memberships;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMembershipPaymentsQueryHandler(
        IMembershipPaymentBindingRepository bindings,
        IPaymentIntentRepository intents,
        IMembershipSubjectAuthority memberships,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _bindings = bindings;
        _intents = intents;
        _memberships = memberships;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<MembershipPaymentBindingResponse>>> Handle(
        GetMembershipPaymentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MembershipPaymentBindingResponse>>(caller.Error);
        }

        var facts = await _memberships.DescribeAsync(query.MembershipId, cancellationToken);

        if (facts is null || facts.MemberUserId != caller.Value)
        {
            return Result.Failure<IReadOnlyList<MembershipPaymentBindingResponse>>(Error.NotFound(
                FinancialErrorCodes.MembershipNotFound,
                "No membership of yours with that identifier."));
        }

        var bindings = await _bindings.ListByMembershipAsync(query.MembershipId, cancellationToken);
        var symbols = await _currencies.AllAsync(cancellationToken);
        var responses = new List<MembershipPaymentBindingResponse>(bindings.Count);

        foreach (var binding in bindings)
        {
            var intent = await _intents.FindAsync(binding.PaymentIntentId, cancellationToken);

            responses.Add(new MembershipPaymentBindingResponse
            {
                BindingId = binding.Id.Value,
                MembershipId = binding.MembershipId,
                PaymentIntentId = binding.PaymentIntentId.Value,
                PaymentStatus = intent?.Status.ToString() ?? PaymentIntentStatus.Settled.ToString(),
                Amount = FinancialMapper.ToResponse(
                    binding.Amount,
                    symbols.TryGetValue(binding.Currency, out var currency) ? currency.Symbol : null),
                SettledAt = binding.SettledAt,
                BoundAt = binding.CreatedAt,
                IsNewBinding = false
            });
        }

        return Result.Success<IReadOnlyList<MembershipPaymentBindingResponse>>(responses);
    }
}
