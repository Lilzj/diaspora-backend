using Diaspora.Platform.Operations.Diagnostics;

namespace Diaspora.Operations.Diagnostics;

/// <summary>
/// The fault injector that ships: one that never injects anything.
/// <para>
/// <see cref="IsActive"/> is a constant <see langword="false"/>, so every call site can guard on it and
/// the JIT will elide the branch. That is what makes it acceptable to put injection points on hot paths:
/// the cost of having them, with this registered, is nothing.
/// </para>
/// <para>
/// The value of the type is not what it does. It is that <c>ExecuteAsync</c>, the cache read path and
/// the queue consumers already call an injector, so running a chaos experiment later is a registration
/// rather than an edit to every one of them.
/// </para>
/// </summary>
public sealed class NoFaultInjector : IFaultInjector
{
    public bool IsActive => false;

    public ValueTask InjectAsync(
        FaultKind kind, string operation, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
