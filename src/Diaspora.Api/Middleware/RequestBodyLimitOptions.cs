using System.ComponentModel.DataAnnotations;

namespace Diaspora.Api.Middleware;

/// <summary>
/// The ceiling on a request body, for every route that does not declare its own — BSEC-006.
/// </summary>
/// <remarks>
/// <para>
/// <b>There was no limit at all.</b> Nothing in code or configuration set
/// <c>KestrelServerOptions.Limits.MaxRequestBodySize</c>, so the only bound was Kestrel's own
/// 30 MB default — a number nobody on this platform chose, and one that applied equally to a
/// two-field PATCH and to a guide draft.
/// </para>
/// <para>
/// <b>Why this is configuration rather than a constant.</b> The audit's remediation says "set a
/// conservative global limit (e.g. 1 MB)". A wrong number here is not a security failure, it is a
/// <c>413</c> for an author mid-draft, and the only way to find that out is in production. A
/// setting can be raised in one line while the right per-route ceiling is worked out; a constant
/// needs a code change.
/// </para>
/// <para>
/// <b>Why 2 MiB and not the suggested 1.</b> 82 of this platform's 303 request contracts carry a
/// collection, and the ceiling has to clear the largest one that is genuinely sent. The largest
/// measured is the candidate profile: <c>JobLimits.MaxExperience</c> (40) ×
/// <c>MaxExperienceDescription</c> (4,000) is 160,000 characters, and at UTF-8's worst case of
/// four bytes per character that is 640 KB before the rest of the body. 1 MB leaves that under
/// two-thirds of headroom. 2 MiB clears it comfortably and is still fifteen times tighter than
/// the default it replaces.
/// </para>
/// <para>
/// <b>Two route families legitimately exceed it</b> and say so at their own registration, because
/// the figure belongs beside the contract it bounds: official notices
/// (<c>OfficialNoticeEndpoints</c>) and guide authoring (<c>GuideEndpoints</c>). Media upload
/// already declared its own under BSEC-005. Nothing else measured above 1 MB.
/// </para>
/// </remarks>
public sealed class RequestBodyLimitOptions
{
    public const string SectionName = "RequestLimits";

    /// <summary>The default ceiling in bytes for a route that declares no limit of its own.</summary>
    /// <remarks>
    /// The range is deliberately narrow at both ends. Below 64 KiB the platform's ordinary
    /// authoring requests start failing. The upper bound is 16 MiB because Kestrel's own default
    /// is 30,000,000 bytes: anything above that would make this setting looser than the default
    /// it exists to tighten, which is worse than absent — it would read as a deliberate choice
    /// while weakening the bound.
    /// </remarks>
    [Range(64 * 1024L, 16 * 1024L * 1024L)]
    public long MaxBodyBytes { get; set; } = 2 * 1024 * 1024;
}
