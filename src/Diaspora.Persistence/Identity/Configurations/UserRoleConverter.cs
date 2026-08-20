using Diaspora.Domain.Identity.Users;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Persistence.Identity.Configurations;

/// <summary>
/// Stores <see cref="UserRole"/> by name, for the same legibility reasons as
/// <see cref="AccessStateConverter"/>.
/// </summary>
public sealed class UserRoleConverter : ValueConverter<UserRole, string>
{
    public const int MaxLength = 40;

    public UserRoleConverter()
        : base(
            role => role.Name,
            name => Diaspora.SharedKernel.Primitives.Enumeration.FromName<UserRole>(name))
    {
    }
}
