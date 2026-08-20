using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Handler for commands with no return value.
/// </summary>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Handler for commands with a typed return value.
/// </summary>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}
