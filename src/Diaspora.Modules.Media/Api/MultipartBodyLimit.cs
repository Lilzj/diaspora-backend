using Microsoft.AspNetCore.Http.Metadata;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// Caps the bytes <c>ReadFormAsync</c> will accept in one multipart section (BSEC-006).
/// <para>
/// <b>Why this type exists rather than the framework's own.</b> ASP.NET Core ships
/// <c>FormOptionsMetadata</c>, which does exactly this and is <b>internal</b> — it appears in the
/// reference XML, because that documents internals too, and does not compile. The interface it
/// implements is public, so the only part worth having is three lines.
/// </para>
/// <para>
/// <b>Why a per-endpoint limit at all.</b> <c>RequestSizeLimitAttribute</c> bounds what Kestrel
/// reads off the wire; this bounds what the form reader will buffer out of it. They are different
/// layers with different defaults, and the form reader's is 128 MB — far above any body this route
/// is allowed to receive. Setting only the first would leave a limit nobody chose sitting behind
/// one somebody did.
/// </para>
/// <para>
/// Every other member is left null on purpose: a null means "use the configured default", so this
/// changes one limit and inherits the rest rather than restating a framework default that may
/// move.
/// </para>
/// </summary>
/// <param name="MultipartBodyLengthLimit">The per-section byte ceiling.</param>
internal sealed record MultipartBodyLimit(long MultipartBodyLengthLimit) : IFormOptionsMetadata
{
    long? IFormOptionsMetadata.MultipartBodyLengthLimit => MultipartBodyLengthLimit;

    bool? IFormOptionsMetadata.BufferBody => null;

    long? IFormOptionsMetadata.BufferBodyLengthLimit => null;

    int? IFormOptionsMetadata.MemoryBufferThreshold => null;

    int? IFormOptionsMetadata.MultipartBoundaryLengthLimit => null;

    int? IFormOptionsMetadata.MultipartHeadersCountLimit => null;

    int? IFormOptionsMetadata.MultipartHeadersLengthLimit => null;

    int? IFormOptionsMetadata.KeyLengthLimit => null;

    int? IFormOptionsMetadata.ValueCountLimit => null;

    int? IFormOptionsMetadata.ValueLengthLimit => null;
}
