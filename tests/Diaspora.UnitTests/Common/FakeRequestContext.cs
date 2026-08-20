using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.UnitTests.Common;

public sealed class FakeRequestContext : IRequestContext
{
    public string CorrelationId { get; set; } = "test-correlation-id";
    public string RequestId { get; set; } = "test-request-id";
    public string? Culture { get; set; }
    public string? Timezone { get; set; }
    public string? DeviceId { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public string? Language { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
