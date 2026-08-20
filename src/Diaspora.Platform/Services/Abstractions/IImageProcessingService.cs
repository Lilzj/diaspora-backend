using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

public interface IImageProcessingService
{
    Task<Stream> CompressAsync(Stream source, int quality = 80, CancellationToken cancellationToken = default);
    Task<Stream> ResizeAsync(Stream source, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);
    Task<Stream> CropAsync(Stream source, int x, int y, int width, int height, CancellationToken cancellationToken = default);
    Task<Stream> GenerateThumbnailAsync(Stream source, int size, CancellationToken cancellationToken = default);
    Task<string> GenerateBlurHashAsync(Stream source, CancellationToken cancellationToken = default);
    Task<ImageMetadata> ExtractMetadataAsync(Stream source, CancellationToken cancellationToken = default);
}
