using System.Reflection;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Application.Behaviors;

/// <summary>
/// Refuses a request marked <see cref="RequiresStepUpAttribute"/> unless the caller's session has
/// re-proved itself recently, in that scope (BSEC-007).
/// <para>
/// <b>Why it sits here and not at the endpoint.</b> The commands that need this are reached from
/// several routes — a withdrawal from the wallet screen, a payout destination from onboarding and
/// from the refresh route the finding singled out — and the gate has to hold on all of them. On the
/// request, it holds wherever the request is sent from, including from a future route nobody has
/// written yet. That is the same argument the platform's own guardrail makes for
/// <c>[Transactional]</c>, which found a live B4 defect by being on the request rather than the
/// handler.
/// </para>
/// <para>
/// <b>Order.</b> After <see cref="AuthorizationBehavior{TRequest,TResponse}"/>, so a caller without
/// the permission is told that and never learns which operations are step-up-guarded. Before
/// <see cref="TransactionBehavior{TRequest,TResponse}"/>, so a refused request never opens one.
/// </para>
/// <para>
/// <b>It fails closed, unlike its neighbour.</b>
/// <see cref="PostingStandingBehavior{TRequest,TResponse}"/> resolves its port optionally and lets
/// the request through when nothing is registered, because a host composed without Trust blocks
/// nobody and that is correct. The opposite is correct here: a host composed without the step-up
/// service must refuse a withdrawal, not wave it past. So the service is resolved with
/// <c>GetRequiredService</c> inside the guarded branch — a misconfigured host fails loudly on the
/// first guarded request rather than quietly serving them all.
/// </para>
/// </summary>
public sealed class StepUpBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _services;

    public StepUpBehavior(IServiceProvider services)
    {
        _services = services;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var scope = typeof(TRequest).GetCustomAttribute<RequiresStepUpAttribute>()?.Scope;

        if (scope is null)
        {
            // The overwhelming majority of requests. Resolved lazily rather than through the
            // constructor so an unguarded request costs one attribute lookup and no service
            // resolution at all.
            return await next();
        }

        var stepUp = _services.GetRequiredService<IStepUpService>();

        if (!await stepUp.HasProvenAsync(scope, cancellationToken))
        {
            throw new Exceptions.StepUpRequiredException(scope);
        }

        return await next();
    }
}
