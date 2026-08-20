using FluentAssertions;
using Xunit;

namespace Diaspora.AnalyzerTests;

/// <summary>
/// What a command may do and what a query may not — including the placement mistake the analyzer
/// itself made first.
/// </summary>
public sealed class CqrsGuardrailAnalyzerTests
{
    private const string Requests =
        """
        public sealed record DoThingCommand : ICommand<string>;

        [Transactional(Reason = "Writes two aggregates.")]
        public sealed record DoTwoThingsCommand : ICommand<string>;

        public sealed record ReadThingQuery : IQuery<string>;
        """;

    // ── DSP4001: a query that writes ─────────────────────────────────────

    [Fact]
    public void A_query_handler_that_saves_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class ReadThingQueryHandler : IQueryHandler<ReadThingQuery, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public ReadThingQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(ReadThingQuery query, CancellationToken ct)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("read");
                }
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP4001");
    }

    /// <summary>
    /// <b>The four read-path counters the platform actually has.</b> A listing, a job, an event and
    /// a home each record the view that the read caused, on purpose, for a reason still good. The
    /// attribute is what makes the exception visible to the next reader instead of invisible to the
    /// rule.
    /// </summary>
    [Fact]
    public void A_query_handler_that_declares_its_side_effect_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            [SideEffecting(Reason = "Records the view this read caused.")]
            public sealed class ReadThingQueryHandler : IQueryHandler<ReadThingQuery, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public ReadThingQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(ReadThingQuery query, CancellationToken ct)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("read");
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP4002: two saves, no transaction ───────────────────────────────

    /// <summary>
    /// Only saves the control flow cannot avoid running one after the other. Two saves in separate
    /// branches of an <c>if</c> are one save per execution, and a textual count of
    /// <c>SaveChangesAsync</c> per file — which is what a grep gives — flags twenty-two handlers in
    /// this platform that are all correct.
    /// </summary>
    [Fact]
    public void Two_sequential_saves_without_a_transactional_command_are_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class DoThingCommandHandler : ICommandHandler<DoThingCommand, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public DoThingCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(DoThingCommand command, CancellationToken ct)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("done");
                }
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP4002");
    }

    /// <summary>
    /// <b>The placement this analyzer got wrong on its first run.</b> It looked for
    /// <c>[Transactional]</c> on the handler and reported two handlers whose commands were correctly
    /// marked. <c>TransactionBehavior</c> closes over <c>TRequest</c>, so the command is the only
    /// place the attribute does anything.
    /// </summary>
    [Fact]
    public void Two_sequential_saves_under_a_transactional_command_are_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class DoTwoThingsCommandHandler : ICommandHandler<DoTwoThingsCommand, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public DoTwoThingsCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(DoTwoThingsCommand command, CancellationToken ct)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("done");
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    [Fact]
    public void Saves_on_separate_branches_are_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class DoThingCommandHandler : ICommandHandler<DoThingCommand, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public DoThingCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(DoThingCommand command, CancellationToken ct)
                {
                    if (command is null)
                    {
                        await _unitOfWork.SaveChangesAsync(ct);
                        return Result.Success("early");
                    }

                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("done");
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP4003: staged and never committed ──────────────────────────────

    /// <summary>
    /// <c>Add</c> marks the change tracker and nothing else. Without a save the handler returns a
    /// successful <c>Result</c>, the API answers 201, and the row was never written — which is the
    /// failure that survives an integration test asserting the response.
    /// </summary>
    [Fact]
    public void A_command_handler_that_stages_and_never_saves_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class DoThingCommandHandler : ICommandHandler<DoThingCommand, string>
            {
                private readonly IThingRepository _things;

                public DoThingCommandHandler(IThingRepository things) => _things = things;

                public Task<Result<string>> Handle(DoThingCommand command, CancellationToken ct)
                {
                    _things.Add(new Thing());
                    return Task.FromResult(Result.Success("done"));
                }
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP4003");
    }

    [Fact]
    public void A_command_handler_that_stages_and_saves_is_left_alone()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            public sealed class DoThingCommandHandler : ICommandHandler<DoThingCommand, string>
            {
                private readonly IThingRepository _things;
                private readonly IUnitOfWork _unitOfWork;

                public DoThingCommandHandler(IThingRepository things, IUnitOfWork unitOfWork)
                {
                    _things = things;
                    _unitOfWork = unitOfWork;
                }

                public async Task<Result<string>> Handle(DoThingCommand command, CancellationToken ct)
                {
                    _things.Add(new Thing());
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("done");
                }
            }
            """));

        ids.Should().BeEmpty();
    }

    // ── DSP4004: the inert attribute ─────────────────────────────────────

    /// <summary>
    /// <b>A live defect this rule found in B4.</b> Location's
    /// <c>DeclareCountryExperienceCommandHandler</c> carried
    /// <c>[Transactional(Reason = "Writes the experience and its expertise claims together.")]</c>
    /// with a class comment asserting a transaction, and no transaction was ever opened — the
    /// attribute was on the handler. That is B2's logging-only transaction behaviour again, in one
    /// class, and nothing had noticed since B4.
    /// </summary>
    [Fact]
    public void Transactional_on_a_handler_is_reported()
    {
        var ids = AnalyzerHarness.Ids(Probes.File(Requests +
            """

            [Transactional(Reason = "Does nothing here.")]
            public sealed class DoThingCommandHandler : ICommandHandler<DoThingCommand, string>
            {
                private readonly IUnitOfWork _unitOfWork;

                public DoThingCommandHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

                public async Task<Result<string>> Handle(DoThingCommand command, CancellationToken ct)
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return Result.Success("done");
                }
            }
            """));

        ids.Should().ContainSingle().Which.Should().Be("DSP4004");
    }
}
