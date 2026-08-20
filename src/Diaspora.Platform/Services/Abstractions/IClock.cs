namespace Diaspora.Platform.Services.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset Now(string timeZoneId);
}
