using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Identifiers;

/// <summary>A report about a job posting.</summary>
public sealed record JobReportId(Guid Value) : StronglyTypedId(Value)
{
    public static JobReportId New() => new(Guid.NewGuid());

    public static JobReportId From(Guid value) => new(value);
}
