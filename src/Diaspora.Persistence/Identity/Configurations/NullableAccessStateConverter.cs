using Diaspora.Domain.Identity.AccessStates;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Nullable counterpart of <see cref="AccessStateConverter"/>, for the "state before the
/// restriction" field that is only set while a member is suspended or locked.
/// </summary>
public sealed class NullableAccessStateConverter : ValueConverter<AccessState?, string?>
{
    public NullableAccessStateConverter()
        : base(
            state => state == null ? null : state.Name,
            name => name == null
                ? null
                : Diaspora.SharedKernel.Primitives.Enumeration.FromName<AccessState>(name))
    {
    }
}
