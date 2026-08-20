namespace Diaspora.Platform.Services.Abstractions;

public interface IStorageService
{
    Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GetUrlAsync(string key, CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}
