namespace Diaspora.Platform.Services.Abstractions;

public interface ICorrelationContext
{
    string CorrelationId { get; }
    string? CausationId { get; }
}
