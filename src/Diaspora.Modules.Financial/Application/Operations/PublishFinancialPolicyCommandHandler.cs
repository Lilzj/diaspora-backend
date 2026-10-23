using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Publishes a policy version and closes whatever it supersedes.
/// <para>
/// <b>Versions are rows, never edits.</b> Version 1 stays in the table for ever because every ledger
/// entry it priced names it; recomputing a historical amount from today's configuration would
/// produce a plausible, wrong number with nothing anywhere reporting a problem.
/// </para>
/// <para>
/// The cache is cleared <em>after</em> the save, for the reason every catalogue handler on this
/// platform records: clearing in front of a change that then failed to commit re-caches the old row.
/// </para>
/// </summary>
public sealed class PublishFinancialPolicyCommandHandler
    : ICommandHandler<PublishFinancialPolicyCommand, FinancialPolicyResponse>
{
    private readonly IFinancialPolicyRepository _policies;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IFinancialCache _cache;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PublishFinancialPolicyCommandHandler(
        IFinancialPolicyRepository policies,
        FinancialAuthorizationService authorization,
        IFinancialCache cache,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _policies = policies;
        _authorization = authorization;
        _cache = cache;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FinancialPolicyResponse>> Handle(
        PublishFinancialPolicyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<FinancialPolicyKind>(command.Kind, true, out var kind))
        {
            return Result.Failure<FinancialPolicyResponse>(Error.Validation(
                FinancialErrorCodes.PolicyNotFound,
                $"'{command.Kind}' is not a policy kind the platform recognises."));
        }

        if (kind == FinancialPolicyKind.RewardConversion
            && await RewardRateRefusalAsync(command.Parameters, cancellationToken) is { } refusal)
        {
            return Result.Failure<FinancialPolicyResponse>(refusal);
        }

        var scope = Enum.TryParse<PolicyScopeKind>(command.Scope, true, out var parsed)
            ? parsed
            : PolicyScopeKind.Platform;

        var now = _clock.UtcNow;
        var effectiveFrom = command.EffectiveFrom ?? now;

        var previous = (await _policies.ListActiveByKeyAsync(command.PolicyKey, cancellationToken))
            .Where(policy => policy.Scope == scope)
            .Where(policy => string.Equals(
                policy.ScopeValue, command.ScopeValue?.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var version = await _policies.HighestVersionAsync(
            command.PolicyKey, scope, command.ScopeValue, cancellationToken) + 1;

        FinancialPolicy policy;

        try
        {
            policy = FinancialPolicy.Publish(
                FinancialPolicyId.From(_guids.NewSequentialGuid()),
                command.PolicyKey,
                kind,
                scope,
                command.ScopeValue,
                version,
                DateWindow.Open(effectiveFrom),
                command.Description,
                now,
                isSystem: false,
                _authorization.UserIdOrNull());
        }
        catch (DomainException exception)
        {
            return Result.Failure<FinancialPolicyResponse>(
                Error.Validation(exception.Code, exception.Message));
        }

        foreach (var (key, value) in command.Parameters)
        {
            policy.SetParameter(_guids.NewSequentialGuid(), key, value);
        }

        _policies.Add(policy);

        foreach (var superseded in previous)
        {
            superseded.Supersede(effectiveFrom);
            _policies.Update(superseded);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.RemoveByPrefixAsync("financial:policies", cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(policy));
    }

    /// <summary>
    /// A positive conversion rate must say what currency it is in.
    /// <para>
    /// <c>minorUnitsPerPoint</c> is a count of minor units, which is a pound or a kobo depending on the
    /// currency. A rate published without one would be applied to whatever currency a caller asked
    /// for — the defect this guard exists to make unpublishable. A zero rate needs no currency: it
    /// converts nothing, which is what the seed relies on.
    /// </para>
    /// </summary>
    private async Task<Error?> RewardRateRefusalAsync(
        IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var rate = Lookup(parameters, FinancialPolicyParameters.MinorUnitsPerPoint);

        if (!decimal.TryParse(
                rate,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedRate)
            || parsedRate <= 0m)
        {
            return null;
        }

        var currency = Lookup(parameters, FinancialPolicyParameters.Currency)?.Trim();

        return !string.IsNullOrEmpty(currency) && await _currencies.IsSupportedAsync(currency, cancellationToken)
            ? null
            : Error.Validation(
                FinancialErrorCodes.RewardCurrencyRequired,
                "A reward conversion rate needs the currency it is in: a supported ISO 4217 code in "
                + $"the '{FinancialPolicyParameters.Currency}' parameter.");
    }

    private static string? Lookup(IReadOnlyDictionary<string, string> parameters, string key) =>
        parameters
            .FirstOrDefault(pair => string.Equals(pair.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
            .Value;
}
