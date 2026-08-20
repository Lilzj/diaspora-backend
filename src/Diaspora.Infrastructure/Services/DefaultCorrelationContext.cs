using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Infrastructure.Services;

public sealed class DefaultCorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();
    private static readonly AsyncLocal<string?> _causationId = new();

    public string CorrelationId
    {
        get => _correlationId.Value ??= Guid.NewGuid().ToString("N");
        set => _correlationId.Value = value;
    }

    public string? CausationId
    {
        get => _causationId.Value;
        set => _causationId.Value = value;
    }
}
