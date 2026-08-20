using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Identifiers;

/// <summary>
/// A listing report's identity. Prefixed, because the kernel's identifier bag already declares a
/// bare <c>ReportId</c>.
/// </summary>
public sealed record ListingReportId(Guid Value) : StronglyTypedId(Value)
{
    public static ListingReportId New() => new(Guid.NewGuid());

    public static ListingReportId From(Guid value) => new(value);
}
