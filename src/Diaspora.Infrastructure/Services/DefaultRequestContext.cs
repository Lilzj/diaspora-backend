using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultRequestContext : IRequestContext
{
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
    public string RequestId { get; } = Guid.NewGuid().ToString("N");
    public string? Culture => null;
    public string? Timezone => null;
    public string? DeviceId => null;
    public string? Platform => null;
    public string? AppVersion => null;
    public string? Language => null;
    public string? IpAddress => null;
    public string? UserAgent => null;
}
