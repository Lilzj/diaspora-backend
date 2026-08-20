namespace Diaspora.Platform.Services.Abstractions;

public interface IRequestContext
{
    string CorrelationId { get; }
    string RequestId { get; }
    string? Culture { get; }
    string? Timezone { get; }
    string? DeviceId { get; }
    string? Platform { get; }
    string? AppVersion { get; }
    string? Language { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
