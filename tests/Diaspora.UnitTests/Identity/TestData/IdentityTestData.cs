using Diaspora.Domain.Identity.Trust;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.UnitTests.Identity.TestData;

/// <summary>
/// Builders for identity aggregates under test. Centralised so a change to the
/// registration signature updates every test at once rather than a hundred call sites.
/// </summary>
public static class IdentityTestData
{
    public static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public const string ValidPhone = "+2348012345678";
    public const string ValidPassword = "Correct-Horse-Battery-7";

    public static User RegisterUser(
        string phone = ValidPhone,
        string fullName = "Amara Okafor",
        string nationality = "NG",
        string residence = "CA",
        string language = "en",
        string timeZone = "UTC",
        DateTimeOffset? now = null) =>
        User.Register(
            FullName.Create(fullName),
            PhoneNumber.Create(phone),
            PasswordHash.Create("hashed::" + ValidPassword),
            CountryCode.Create(nationality),
            CountryCode.Create(residence),
            LanguageCode.Create(language),
            TimeZoneIdentifier.Create(timeZone),
            now ?? Now);

    /// <summary>A member who has completed phone verification and holds limited access.</summary>
    public static User LimitedAccessUser(DateTimeOffset? now = null)
    {
        var moment = now ?? Now;
        var user = RegisterUser(now: moment);
        user.BeginPhoneVerification(moment);
        user.ConfirmPhoneVerified(moment);
        user.ClearDomainEvents();
        return user;
    }

    /// <summary>A member the Trust Engine has verified.</summary>
    public static User VerifiedUser(DateTimeOffset? now = null)
    {
        var moment = now ?? Now;
        var user = LimitedAccessUser(moment);
        user.RequestVerification(moment);
        user.ApplyTrustDecision(TrustDecision.Create(TrustDecisionOutcome.UnderReview, moment), moment);
        user.ApplyTrustDecision(TrustDecision.Create(TrustDecisionOutcome.Granted, moment), moment);
        user.ClearDomainEvents();
        return user;
    }

    public static SecretHash Hash(string value) => SecretHash.Create("sha::" + value);
}
