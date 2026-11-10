using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// DSP4005 — a request must declare what it requires of its caller (BSOLID-001).
/// <para>
/// <b>Why an analyzer when the runtime already refuses it.</b> <c>AuthorizationBehavior</c> throws
/// on an unannotated request, so the platform is safe either way. What the analyzer buys is
/// <em>where</em> the author finds out: a squiggle while they are writing the request, rather than
/// a 401 on a route that has already shipped. BSEC-003's confirmed instance went unnoticed for as
/// long as the route existed precisely because nothing objected at the keystroke.
/// </para>
/// </summary>
public sealed class AuthorizationDeclarationAnalyzerTests
{
    [Fact]
    public void A_command_that_declares_nothing_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed record DoThingCommand : ICommand<string>;
            """));

        ids.Should().Contain("DSP4005");
    }

    [Fact]
    public void A_query_that_declares_nothing_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed record ReadThingQuery : IQuery<string>;
            """));

        ids.Should().Contain("DSP4005");
    }

    /// <summary>
    /// The parameterless <c>ICommand</c> too, which returns a bare <c>Result</c> and is the shape
    /// a write with nothing to report takes.
    /// </summary>
    [Fact]
    public void A_command_with_no_response_that_declares_nothing_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed record DoThingCommand : ICommand;
            """));

        ids.Should().Contain("DSP4005");
    }

    [Theory]
    [InlineData("[RequirePermission(\"probe:things:read\")]")]
    [InlineData("[RequirePolicy(\"probe.things.read\")]")]
    [InlineData("[RequireAuthentication(\"The caller's own things.\")]")]
    [InlineData("[AllowAnonymous(\"Published reference content.\")]")]
    public void Any_one_of_the_four_declarations_satisfies_the_rule(string attribute)
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            attribute + """

            public sealed record ReadThingQuery : IQuery<string>;
            """));

        ids.Should().NotContain("DSP4005");
    }

    /// <summary>
    /// <b>A type that is not a request is not reported</b>, which is the half that keeps the rule
    /// usable. A module declares hundreds of records — responses, value objects, options — and a
    /// rule that asked them all to carry an authorization attribute would be turned off by the
    /// second person who hit it.
    /// </summary>
    [Fact]
    public void A_record_that_is_not_a_request_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            public sealed record ThingResponse(string Name);
            """));

        ids.Should().NotContain("DSP4005");
    }

    /// <summary>
    /// A handler is not a request either. The attribute belongs on the type MediatR closes the
    /// pipeline over, and reporting the handler would send the author to the wrong file.
    /// </summary>
    [Fact]
    public void A_handler_is_not_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(
            """
            [RequirePermission("probe:things:read")]
            public sealed record ReadThingQuery : IQuery<string>;

            public sealed class ReadThingQueryHandler : IQueryHandler<ReadThingQuery, string>
            {
                public Task<Result<string>> Handle(
                    ReadThingQuery query, CancellationToken cancellationToken) =>
                    Task.FromResult(Result.Success(string.Empty));
            }
            """));

        ids.Should().NotContain("DSP4005");
    }
}
