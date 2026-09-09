using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository" />.
/// <para>
/// Value-object properties are compared against value objects, not against their underlying
/// strings. The properties carry value converters, and EF applies the converter to the
/// other operand too — so <c>EF.Property&lt;string&gt;(user, "PhoneNumber") == "+234..."</c>
/// throws at execution time trying to cast the string into a PhoneNumber. Comparing like
/// with like lets EF convert the parameter and keeps the filter in SQL, where the unique
/// index serves it.
/// </para>
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly DiasporaDbContext _dbContext;

    public UserRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<User> Users => _dbContext.Set<User>();

    public async Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default) =>
        await Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var ids = userIds.Where(id => id is not null).Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByIdWithPasswordHistoryAsync(
        UserId userId,
        CancellationToken cancellationToken = default) =>
        await Users
            .Include(user => user.PasswordHistory)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public async Task<User?> GetByPhoneNumberAsync(
        PhoneNumber phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return await Users.FirstOrDefaultAsync(
            user => user.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<User?> GetByPhoneNumberWithPasswordHistoryAsync(
        PhoneNumber phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return await Users
            .Include(user => user.PasswordHistory)
            .FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        return await Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<User?> GetByEmailWithPasswordHistoryAsync(
        EmailAddress email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        return await Users
            .Include(user => user.PasswordHistory)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public async Task<bool> ExistsByPhoneNumberAsync(
        PhoneNumber phoneNumber,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumber);

        return await Users.AnyAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);
    }

    public async Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);

        return await Users.AnyAsync(user => user.Email == email, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await Users.AddAsync(user, cancellationToken);

    public void Update(User user) =>
        TrackedEntityUpdater.MarkModified(_dbContext, Users, user);
}
