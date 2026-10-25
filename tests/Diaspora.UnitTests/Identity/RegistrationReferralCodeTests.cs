using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Commands.Registration;
using Diaspora.Application.Identity.Validation;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.SharedKernel.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity;

/// <summary>
/// A registration can carry the code of the member who invited them (RWD-03).
/// <para>
/// Identity checks the code's shape and announces it. Whether it belongs to anybody is the referral
/// programme's question, answered after the account exists and in silence either way.
/// </para>
/// </summary>
public sealed class RegistrationReferralCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private sealed class EnglishMessages : ILocalizedMessageProvider
    {
        public string GetMessage(string key) => IdentityMessageCatalog.Resolve(key, "en");

        public string GetMessage(string key, params object[] arguments) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, GetMessage(key), arguments);

        public string GetFor(string key, string? language) => IdentityMessageCatalog.Resolve(key, language);
    }

    private static RegisterUserCommand Registration(string? referralCode) => new()
    {
        FullName = "Amara Okafor",
        PhoneNumber = "+2348012345678",
        Email = "amara@diaspora.test",
        Password = "Correct-Horse-Battery-7",
        Nationality = "NG",
        CountryOfResidence = "CA",
        ReferralCode = referralCode
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB12CD34EF")]
    [InlineData(" ab12cd34ef ")]
    public void A_registration_with_no_code_or_a_well_formed_one_is_valid(string? code)
    {
        new RegisterUserCommandValidator(new EnglishMessages()).Validate(Registration(code))
            .Errors.Should().NotContain(error => error.PropertyName == nameof(RegisterUserCommand.ReferralCode));
    }

    [Theory]
    [InlineData("AB-12")]
    [InlineData("not a code")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void A_code_with_anything_but_letters_and_numbers_is_refused(string code)
    {
        new RegisterUserCommandValidator(new EnglishMessages()).Validate(Registration(code))
            .Errors.Should().ContainSingle(error => error.PropertyName == nameof(RegisterUserCommand.ReferralCode))
            .Which.ErrorMessage.Should().Be("A referral code has only letters and numbers.");
    }

    [Fact]
    public void The_registration_announces_the_code_trimmed()
    {
        var user = User.Register(
            FullName.Create("Amara Okafor"),
            PhoneNumber.Create("+447700900123"),
            PasswordHash.Create(new string('a', 60)),
            CountryCode.Create("NG"),
            CountryCode.Create("GB"),
            LanguageCode.Create("en"),
            TimeZoneIdentifier.Create("Europe/London"),
            Now,
            "  AB12CD34EF ");

        user.DomainEvents.OfType<UserRegisteredDomainEvent>().Single()
            .ReferralCode.Should().Be("AB12CD34EF");
    }
}
