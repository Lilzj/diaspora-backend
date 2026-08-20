using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Releases an asset a scanner flagged. Admins only, and always audited.
/// <para>
/// Exists because false positives are real: a scanner signature update occasionally flags a
/// legitimate PDF, and the member whose immigration document was quarantined has a real
/// problem. It is deliberately the narrowest capability in the module — a role check, an audit
/// record, and a reason nobody can omit.
/// </para>
/// </summary>
[Auditable]
public sealed record ReleaseQuarantineCommand : ICommand<MediaAssetResponse>
{
    public required Guid MediaId { get; init; }

    /// <summary>Why. Recorded on the asset's scan result and in the audit trail.</summary>
    public required string Reason { get; init; }
}
