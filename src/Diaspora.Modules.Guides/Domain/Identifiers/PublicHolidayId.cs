using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one public holiday or official closure.
/// <para>
/// One aggregate per observance per year, not per recurring rule. A recurring rule would have to
/// encode "the first Monday after the full moon following the vernal equinox" and every national
/// exception to it, and would still be wrong the year a government moves a holiday to make a long
/// weekend — which is exactly the year somebody needs the answer.
/// </para>
/// </summary>
public sealed record PublicHolidayId(Guid Value) : StronglyTypedId(Value)
{
    public static PublicHolidayId New() => new(Guid.NewGuid());

    public static PublicHolidayId From(Guid value) => new(value);
}
