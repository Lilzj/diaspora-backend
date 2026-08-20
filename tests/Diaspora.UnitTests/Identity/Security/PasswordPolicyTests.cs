using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Application.Identity.Validation;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Passwords;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Identity.Security;

public class PasswordPolicyTests
{
    private sealed class EnglishMessages : ILocalizedMessageProvider
    {
        public string GetMessage(string key) => IdentityMessageCatalog.Resolve(key, "en");

        public string GetMessage(string key, params object[] arguments) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, GetMessage(key), arguments);

        public string GetFor(string key, string? language) => IdentityMessageCatalog.Resolve(key, language);
    }

    private static ConfigurablePasswordPolicy CreatePolicy(PasswordPolicyOptions? options = null) =>
        new(Options.Create(options ?? new PasswordPolicyOptions()), new EnglishMessages());

    [Fact]
    public void A_strong_password_is_accepted()
    {
        CreatePolicy().Validate("Correct-Horse-Battery-7", PasswordPolicyContext.Empty)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A_short_password_is_refused()
    {
        var result = CreatePolicy().Validate("Short-1a", PasswordPolicyContext.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.TooShort");
        result.Error.Description.Should().Contain("12");
    }

    [Fact]
    public void An_absurdly_long_password_is_refused()
    {
        // Not a constraint on the member: an unbounded input turns the deliberately slow
        // hash into a denial-of-service vector.
        var result = CreatePolicy().Validate(new string('a', 500), PasswordPolicyContext.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.TooLong");
    }

    [Fact]
    public void Missing_character_classes_are_reported()
    {
        var policy = CreatePolicy();

        policy.Validate("alllowercase-nodigits", PasswordPolicyContext.Empty)
            .Error.Code.Should().Be("Identity.Password.NeedsUppercase");

        policy.Validate("ALLUPPERCASE-NODIGITS", PasswordPolicyContext.Empty)
            .Error.Code.Should().Be("Identity.Password.NeedsLowercase");

        policy.Validate("NoDigitsInThisOne", PasswordPolicyContext.Empty)
            .Error.Code.Should().Be("Identity.Password.NeedsDigit");
    }

    [Fact]
    public void Symbols_are_optional_by_default()
    {
        CreatePolicy().Validate("NoSymbolsHere123", PasswordPolicyContext.Empty)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Symbols_can_be_required_by_configuration()
    {
        var policy = CreatePolicy(new PasswordPolicyOptions { RequireNonAlphanumeric = true });

        policy.Validate("NoSymbolsHere123", PasswordPolicyContext.Empty)
            .Error.Code.Should().Be("Identity.Password.NeedsSymbol");
    }

    [Theory]
    [InlineData("Password123456")]
    [InlineData("MyPassw0rd2026")]
    [InlineData("Qwertyuiop1234")]
    [InlineData("Iloveyou2026Xy")]
    public void An_obviously_weak_password_is_refused_even_when_it_satisfies_the_structure(string password)
    {
        // Each of these clears every character-class rule and the length floor. Exact
        // denylist matching would let all four through.
        var result = CreatePolicy().Validate(password, PasswordPolicyContext.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.TooCommon");
    }

    [Fact]
    public void Weak_stems_are_long_enough_not_to_cause_false_rejections()
    {
        ConfigurablePasswordPolicy.WeakStems.Should().OnlyContain(
            stem => stem.Length >= ConfigurablePasswordPolicy.MinimumWeakStemLength,
            "a short stem matched as a substring would reject reasonable passwords");
    }

    [Fact]
    public void A_password_containing_the_members_name_is_refused()
    {
        var result = CreatePolicy().Validate(
            "AmaraOkafor2026!",
            new PasswordPolicyContext { FullName = "Amara Okafor" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.PersonalInformation");
    }

    [Fact]
    public void A_password_containing_the_members_phone_number_is_refused()
    {
        var result = CreatePolicy().Validate(
            "MyNumberIs345678Ok",
            new PasswordPolicyContext { PhoneNumber = "+2348012345678" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.PersonalInformation");
    }

    [Fact]
    public void Short_name_fragments_do_not_cause_false_rejections()
    {
        // A two-letter name part would otherwise reject almost every password.
        var result = CreatePolicy().Validate(
            "Correct-Horse-Battery-7",
            new PasswordPolicyContext { FullName = "Li Wu" });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_personal_information_rule_can_be_disabled()
    {
        var policy = CreatePolicy(new PasswordPolicyOptions { ForbidPersonalInformation = false });

        policy.Validate("AmaraOkafor2026!", new PasswordPolicyContext { FullName = "Amara Okafor" })
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void The_policy_describes_itself_for_clients()
    {
        var descriptor = CreatePolicy().Describe();

        descriptor.MinimumLength.Should().Be(12);
        descriptor.RequiresUppercase.Should().BeTrue();
        descriptor.RequiresNonAlphanumeric.Should().BeFalse();
        descriptor.HistoryDepth.Should().Be(5);
    }

    [Fact]
    public void An_empty_password_is_refused()
    {
        CreatePolicy().Validate("   ", PasswordPolicyContext.Empty)
            .Error.Code.Should().Be("Identity.Password.Required");
    }
}
