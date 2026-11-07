using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>
/// Persistence contract for the <see cref="User"/> aggregate.
/// <para>
/// Deliberately not derived from the kernel's <c>IRepository&lt;T&gt;</c>, whose
/// <c>GetByIdAsync(Guid)</c> would reintroduce the primitive obsession the strongly
/// typed identifiers exist to prevent.
/// </para>
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a member together with password history, for flows that must enforce
    /// the reuse policy.
    /// </summary>
    /// <summary>
    /// The members among these identifiers, in one round trip.
    /// <para>
    /// For callers that hold a page of identifiers and need a name for each — a list of
    /// conversations, a page of comments. Asking one at a time is the same query twenty times, and
    /// a repository with only a single-row read invites exactly that.
    /// </para>
    /// <para>
    /// An identifier with no member is simply absent from the result, which is the only honest
    /// answer to "is this a person".
    /// </para>
    /// </summary>
    Task<IReadOnlyList<User>> GetByIdsAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithPasswordHistoryAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<User?> GetByPhoneNumberAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default);

    Task<User?> GetByPhoneNumberWithPasswordHistoryAsync(
        PhoneNumber phoneNumber,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same lookup with the password history loaded, for the reset path.
    /// </summary>
    /// <remarks>
    /// Added when recovery moved from the phone number to the email address. The reset
    /// handler has to check the new password against the member's history, and a
    /// history-less load would silently let a member reuse a password the policy
    /// forbids — a refusal that never fires is worse than no policy at all.
    /// </remarks>
    Task<User?> GetByEmailWithPasswordHistoryAsync(
        EmailAddress email, CancellationToken cancellationToken = default);

    Task<bool> ExistsByPhoneNumberAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(EmailAddress email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspended members whose suspension had an end date that has now passed, oldest first, for the
    /// sweep that reinstates them (7 Nov 2026: nothing ended a dated suspension).
    /// </summary>
    Task<IReadOnlyList<User>> ListEndedSuspensionsAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);
}
