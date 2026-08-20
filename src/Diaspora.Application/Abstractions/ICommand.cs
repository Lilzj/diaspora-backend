using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Command with no return value.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

/// <summary>
/// Command that returns a typed result.
/// </summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
