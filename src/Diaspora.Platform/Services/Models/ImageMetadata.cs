namespace Diaspora.Platform.Services.Models;

public sealed record ImageMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string? MimeType { get; init; }
    public long SizeBytes { get; init; }
}
