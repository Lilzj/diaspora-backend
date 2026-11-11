namespace Diaspora.Application.Exceptions;

/// <summary>
/// The caller is who they say they are and may do this, but has not proved it recently enough.
/// <para>
/// <b>Why this is not a <see cref="ForbiddenException"/>, although it is also a 403.</b> The two
/// refusals ask the client for opposite things. "You lack the permission" is final: the only
/// sensible response is to stop and say so. "You have not stepped up" is an instruction: request a
/// code, collect it from the member, and retry. A client that cannot tell them apart shows a dead
/// end where there is a next step — and every refusal on the platform mapped to the same
/// <c>Platform.Auth.Forbidden</c> code until this type existed.
/// </para>
/// <para>
/// <b>It carries the scope</b>, because the client must name one when it asks for a code and
/// guessing is how a flow ends up asking for the wrong proof. The scope travels in the problem
/// document's <c>stepUpScope</c> extension, the same mechanism the rate limiter uses for
/// <c>retryAfter</c>.
/// </para>
/// </summary>
public sealed class StepUpRequiredException : Exception
{
    public StepUpRequiredException(string scope)
        : base("This needs you to confirm it with a code we send to your phone. "
               + "Ask for a code, then try again.")
    {
        Scope = scope;
    }

    /// <summary>The scope a grant must cover for the request to proceed.</summary>
    public string Scope { get; }
}
