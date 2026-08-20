using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>
/// A job posting's identity.
/// <para>
/// <b>Named <c>JobPostingId</c> rather than <c>JobId</c> deliberately.</b> The kernel's legacy
/// identifier bag (<c>Diaspora.SharedKernel.Identity</c>) already declares a <c>JobId</c> from
/// before this module existed. B8 hit the same collision with <c>GuideId</c> and resolved it
/// with a <c>using</c> alias at the top of forty files; naming the module-local type for what
/// it actually identifies avoids the collision instead of managing it — and it is the more
/// accurate name anyway, because "a job" in this domain is an opening, whereas the aggregate
/// is the posting that advertises it.
/// </para>
/// </summary>
public sealed record JobPostingId(Guid Value) : StronglyTypedId(Value)
{
    public static JobPostingId New() => new(Guid.NewGuid());

    public static JobPostingId From(Guid value) => new(value);
}
