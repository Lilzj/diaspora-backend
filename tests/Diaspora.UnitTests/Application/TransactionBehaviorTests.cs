using Diaspora.Application.Abstractions;
using Diaspora.Application.Behaviors;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Application;

/// <summary>
/// The transaction behavior is the fix for B2's placeholder that logged a transaction it
/// never opened. These tests pin the two decisions that make it safe: it wraps only
/// commands that opt in, and it never wraps anything else.
/// </summary>
public class TransactionBehaviorTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRequestContext _requestContext = Substitute.For<IRequestContext>();

    public TransactionBehaviorTests()
    {
        _requestContext.CorrelationId.Returns("test-correlation");

        // ExecuteTransactionalAsync runs the supplied operation and returns its result, so
        // the behavior's own logic is what is under test rather than EF.
        _unitOfWork.HasActiveTransaction.Returns(false);
        _unitOfWork
            .ExecuteTransactionalAsync(Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var operation = callInfo.Arg<Func<CancellationToken, Task<Result>>>();
                return operation(CancellationToken.None);
            });
    }

    [Fact]
    public async Task A_transactional_command_is_run_through_the_unit_of_work()
    {
        var behavior = Behavior<TransactionalCommand>();
        var handlerRan = false;

        await behavior.Handle(
            new TransactionalCommand(),
            () => { handlerRan = true; return Task.FromResult(Result.Success()); },
            CancellationToken.None);

        handlerRan.Should().BeTrue();
        await _unitOfWork.Received(1).ExecuteTransactionalAsync(
            Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_plain_command_is_not_wrapped_in_a_transaction()
    {
        var behavior = Behavior<PlainCommand>();

        await behavior.Handle(
            new PlainCommand(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        await _unitOfWork.DidNotReceive().ExecuteTransactionalAsync(
            Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_transactional_command_already_inside_a_transaction_joins_it()
    {
        _unitOfWork.HasActiveTransaction.Returns(true);
        var behavior = Behavior<TransactionalCommand>();

        await behavior.Handle(
            new TransactionalCommand(),
            () => Task.FromResult(Result.Success()),
            CancellationToken.None);

        // Nested: it runs the handler directly and does not open a second transaction.
        await _unitOfWork.DidNotReceive().ExecuteTransactionalAsync(
            Arg.Any<Func<CancellationToken, Task<Result>>>(), Arg.Any<CancellationToken>());
    }

    private TransactionBehavior<TRequest, Result> Behavior<TRequest>()
        where TRequest : notnull =>
        new(_unitOfWork, _requestContext,
            NullLogger<TransactionBehavior<TRequest, Result>>.Instance);

    // Both declare their authorization because DSP4006 requires it of any ICommand, and in Release
    // a warning is an error. A fixture that could not exist in the platform would be testing
    // something the platform cannot do.
    [RequirePermission("probe:things:write")]
    [Transactional(Reason = "test")]
    private sealed record TransactionalCommand : ICommand;

    [RequirePermission("probe:things:write")]
    private sealed record PlainCommand : ICommand;
}
