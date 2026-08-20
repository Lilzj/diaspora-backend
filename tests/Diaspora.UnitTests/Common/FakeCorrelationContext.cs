using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.UnitTests.Common;

/// <summary>
/// A fixed correlation trail, so a test can assert that the identifier captured at enqueue is
/// the one that reaches the consumer.
/// </summary>
public sealed class FakeCorrelationContext : ICorrelationContext
{
    public FakeCorrelationContext(string correlationId = "test-correlation", string? causationId = null)
    {
        CorrelationId = correlationId;
        CausationId = causationId;
    }

    public string CorrelationId { get; set; }

    public string? CausationId { get; set; }
}
