using Diaspora.Platform.Http;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// BSOLID-006. One reader for the <c>Idempotency-Key</c> header, where there were four.
/// <para>
/// The behaviour below is what all four copies did, asserted once so the next module to need it
/// inherits it instead of writing a fifth. The two reading modes are kept apart on purpose:
/// <see cref="IdempotencyHeader.From"/> for the modules that merge the body field later, inside the
/// normaliser that also scopes the key by operation and caller, and
/// <see cref="IdempotencyHeader.Resolve"/> for the one module that has no normaliser.
/// </para>
/// </summary>
public sealed class IdempotencyHeaderTests
{
    [Fact]
    public void The_header_is_read_under_the_name_the_client_sends()
    {
        IdempotencyHeader.Name.Should().Be(
            "Idempotency-Key",
            "the published contract documents this spelling and the app sends it");

        IdempotencyHeader.From(Request(("Idempotency-Key", "abc-123")))
            .Should().Be("abc-123");
    }

    [Fact]
    public void A_request_without_the_header_has_no_key()
    {
        IdempotencyHeader.From(new DefaultHttpContext()).Should().BeNull();
    }

    /// <summary>
    /// Returned exactly as received. Financial writes this straight into a unique index column and
    /// the modules that normalise trim it themselves, so trimming here would change what one of
    /// them stores without changing what the other does.
    /// </summary>
    [Fact]
    public void The_value_is_not_tidied_up_on_the_way_through()
    {
        IdempotencyHeader.From(Request(("Idempotency-Key", "  padded  ")))
            .Should().Be("  padded  ");
    }

    [Fact]
    public void The_header_wins_over_a_body_field()
    {
        IdempotencyHeader.Resolve(Request(("Idempotency-Key", "from-header")), "from-body")
            .Should().Be("from-header");
    }

    /// <summary>
    /// Whitespace counts as absent for the fallback, which is the behaviour Financial's copy had:
    /// a header present but empty must not beat a body field that carries a real key.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_header_falls_back_to_the_body(string header)
    {
        IdempotencyHeader.Resolve(Request(("Idempotency-Key", header)), "from-body")
            .Should().Be("from-body");
    }

    [Fact]
    public void With_neither_there_is_no_key()
    {
        IdempotencyHeader.Resolve(new DefaultHttpContext(), body: null).Should().BeNull();
    }

    private static DefaultHttpContext Request(params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();

        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return context;
    }
}
