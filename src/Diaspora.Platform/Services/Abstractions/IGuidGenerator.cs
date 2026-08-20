namespace Diaspora.Platform.Services.Abstractions;

public interface IGuidGenerator
{
    Guid NewGuid();
    Guid NewSequentialGuid();
}
