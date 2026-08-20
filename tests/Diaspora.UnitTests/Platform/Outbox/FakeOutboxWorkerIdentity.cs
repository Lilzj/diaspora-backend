using Diaspora.Platform.Messaging.Outbox.Abstractions;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>A stable worker identity, so an assertion about a lease can name its owner.</summary>
public sealed class FakeOutboxWorkerIdentity : IOutboxWorkerIdentity
{
    public FakeOutboxWorkerIdentity(string workerId = "test-worker/1") => WorkerId = workerId;

    public string WorkerId { get; }
}
