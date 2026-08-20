using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Application.Abstractions;

/// <summary>
/// Handler for queries with a typed return value.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
