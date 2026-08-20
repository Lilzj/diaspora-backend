using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
