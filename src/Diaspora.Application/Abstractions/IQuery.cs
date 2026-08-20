using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Query that returns a typed result.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
