using Diaspora.Application.Identity.Validation;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Validation;

public class IdentityMessageCatalogTests
{
    [Fact]
    public void Every_supported_language_covers_every_key()
    {
        // A gap here would silently fall back to English for one message in an otherwise
        // translated flow, which reads as a bug to the member.
        var keys = typeof(IdentityMessageKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        keys.Should().NotBeEmpty();

        foreach (var language in IdentityMessageCatalog.SupportedLanguages)
        {
            foreach (var key in keys)
            {
                IdentityMessageCatalog.Resolve(key, language)
                    .Should().NotBe(key, $"'{key}' is missing a translation for '{language}'");
            }
        }
    }

    [Fact]
    public void Messages_resolve_in_the_requested_language()
    {
        IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "fr")
            .Should().Be("Un numéro de téléphone est requis.");
    }

    [Fact]
    public void A_regional_tag_falls_back_to_its_base_language()
    {
        IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "pt-BR")
            .Should().Be(IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "pt"));
    }

    [Fact]
    public void An_unsupported_language_falls_back_to_english()
    {
        IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "sw")
            .Should().Be(IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "en"));
    }

    [Fact]
    public void A_null_language_falls_back_to_english()
    {
        IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, null)
            .Should().Be(IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "en"));
    }

    [Fact]
    public void An_unknown_key_returns_itself_so_the_gap_is_visible()
    {
        IdentityMessageCatalog.Resolve("identity.validation.does.not.exist", "en")
            .Should().Be("identity.validation.does.not.exist");
    }

    [Fact]
    public void Language_matching_is_case_insensitive()
    {
        IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "FR")
            .Should().Be(IdentityMessageCatalog.Resolve(IdentityMessageKeys.PhoneRequired, "fr"));
    }
}
