using Diaspora.Domain.Identity.AccessStates;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Stores <see cref="AccessState"/> as its name rather than its numeric id.
/// <para>
/// A row that reads "LimitedAccess" is legible to anyone looking at the database during an
/// incident; a row that reads "3" is not. Names also survive reordering the enumeration,
/// which numeric ids would not.
/// </para>
/// </summary>
public sealed class AccessStateConverter : ValueConverter<AccessState, string>
{
    public const int MaxLength = 40;

    public AccessStateConverter()
        : base(
            state => state.Name,
            name => Diaspora.SharedKernel.Primitives.Enumeration.FromName<AccessState>(name))
    {
    }
}
