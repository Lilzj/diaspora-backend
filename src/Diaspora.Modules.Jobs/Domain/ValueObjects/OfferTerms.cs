using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// What an employer is actually offering.
/// <para>
/// <b>The salary here is required, unlike the one on a posting.</b> A posting may decline to
/// publish a figure; an offer may not. A candidate being asked to accept a job — very often to
/// move country for it — is entitled to the number in writing, and an offer aggregate that
/// permitted "salary to be discussed" would be the platform lending its record-keeping to the
/// oldest trick in recruitment.
/// </para>
/// <para>
/// <b>The response deadline is a real date the aggregate enforces.</b> An offer with no expiry
/// leaves a candidate unable to tell whether it still stands, and leaves the employer's pipeline
/// permanently open. Expiry is reached by the clock, not by anyone remembering.
/// </para>
/// <para>
/// This module computes no tax and no take-home figure — international tax calculation is
/// explicitly out of scope, and a platform that guessed at it would be giving advice it cannot
/// stand behind.
/// </para>
/// </summary>
public sealed class OfferTerms : ValueObject
{
    private OfferTerms(
        decimal salaryAmount,
        string currencyCode,
        SalaryPeriod period,
        DateOnly? startDate,
        DateTimeOffset expiresAt,
        string? benefitsSummary,
        string? notes,
        bool includesRelocation,
        bool includesVisaSponsorship)
    {
        SalaryAmount = salaryAmount;
        CurrencyCode = currencyCode;
        Period = period;
        StartDate = startDate;
        ExpiresAt = expiresAt;
        BenefitsSummary = benefitsSummary;
        Notes = notes;
        IncludesRelocation = includesRelocation;
        IncludesVisaSponsorship = includesVisaSponsorship;
    }

    private OfferTerms() { }

    public decimal SalaryAmount { get; private set; }

    /// <summary>Upper-case ISO 4217. Always present.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    public SalaryPeriod Period { get; private set; }

    /// <summary>The proposed first day. Null when it is still to be agreed.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>When the offer lapses if the candidate has not answered.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public string? BenefitsSummary { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Whether relocation support is part of the offer, not just the advert.</summary>
    public bool IncludesRelocation { get; private set; }

    /// <summary>Whether visa sponsorship is part of the offer, not just the advert.</summary>
    public bool IncludesVisaSponsorship { get; private set; }

    public static Result<OfferTerms> Create(
        decimal salaryAmount,
        string? currencyCode,
        SalaryPeriod period,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        DateOnly? startDate = null,
        string? benefitsSummary = null,
        string? notes = null,
        bool includesRelocation = false,
        bool includesVisaSponsorship = false)
    {
        if (salaryAmount < 0)
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferTermsInvalid);
        }

        if (!Enum.IsDefined(period))
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferTermsInvalid);
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return Result.Failure<OfferTerms>(JobErrors.CurrencyInvalid);
        }

        var currency = currencyCode.Trim().ToUpperInvariant();

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<OfferTerms>(JobErrors.CurrencyInvalid);
        }

        if (expiresAt <= now)
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferExpiryInPast);
        }

        if (startDate is { } start && start < DateOnly.FromDateTime(now.UtcDateTime))
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferStartDateInPast);
        }

        var benefits = Clean(benefitsSummary);
        var note = Clean(notes);

        if (benefits is { Length: > JobLimits.MaxOfferNote }
            || note is { Length: > JobLimits.MaxOfferNote })
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferTermsInvalid);
        }

        return Result.Success(new OfferTerms(
            decimal.Round(salaryAmount, 2),
            currency,
            period,
            startDate,
            expiresAt,
            benefits,
            note,
            includesRelocation,
            includesVisaSponsorship));
    }

    /// <summary>Whether the response deadline has passed.</summary>
    public bool HasExpired(DateTimeOffset now) => ExpiresAt <= now;

    /// <summary>
    /// The same terms with a later deadline. An employer extending a deadline is the common
    /// case and must not require withdrawing and re-issuing the offer.
    /// </summary>
    public Result<OfferTerms> WithDeadline(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (expiresAt <= now)
        {
            return Result.Failure<OfferTerms>(JobErrors.OfferExpiryInPast);
        }

        return Result.Success(new OfferTerms(
            SalaryAmount, CurrencyCode, Period, StartDate, expiresAt,
            BenefitsSummary, Notes, IncludesRelocation, IncludesVisaSponsorship));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SalaryAmount;
        yield return CurrencyCode;
        yield return Period;
        yield return StartDate;
        yield return ExpiresAt;
        yield return BenefitsSummary;
        yield return Notes;
        yield return IncludesRelocation;
        yield return IncludesVisaSponsorship;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
