using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Currency;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Resolves a member's currency from the money they hold, then where they live, then the default.
/// See <see cref="IMemberCurrency"/> for why each step is in that order.
/// </summary>
public sealed class MemberCurrency : IMemberCurrency
{
    private readonly IFinancialAccountRepository _accounts;
    private readonly IMemberProfileService _members;
    private readonly FinancialIdentityService _identities;

    public MemberCurrency(
        IFinancialAccountRepository accounts,
        IMemberProfileService members,
        FinancialIdentityService identities)
    {
        _accounts = accounts;
        _members = members;
        _identities = identities;
    }

    public Task<string> ForMemberAsync(Guid userId, CancellationToken cancellationToken = default) =>
        ForOwnerAsync(userId, FinancialAccountType.Personal, countryCode: null, cancellationToken);

    /// <summary>
    /// The currency for an owner of a kind of account.
    /// <para>
    /// An existing account's currency wins over everything. Then an explicit country, which a staff
    /// caller opening an organization's wallet may name. Then — for a personal account, whose owner
    /// is a member — the member's country of residence. Then the configured default.
    /// </para>
    /// </summary>
    public async Task<string> ForOwnerAsync(
        Guid ownerId,
        FinancialAccountType accountType,
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var existing = await _accounts.FindByOwnerAsync(ownerId, accountType, cancellationToken);

        if (Money.NormalizeCurrency(existing?.PrimaryCurrency) is { } held)
        {
            return held;
        }

        if (Money.NormalizeCurrency(CountryCurrency.For(countryCode)) is { } named)
        {
            return named;
        }

        if (accountType == FinancialAccountType.Personal && _members.IsAvailable)
        {
            var profiles = await _members.ResolveAsync([UserId.From(ownerId)], cancellationToken);

            if (profiles.TryGetValue(ownerId, out var profile)
                && Money.NormalizeCurrency(CountryCurrency.For(profile.CountryOfResidence)) is { } resident)
            {
                return resident;
            }
        }

        return _identities.DefaultCurrency;
    }
}
