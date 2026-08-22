using System.Globalization;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Sessions;
using Diaspora.Domain.Identity.Users;
using Diaspora.Domain.Identity.Verification;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity;

/// <summary>
/// What Identity holds about a member, and what happens to it when they ask to leave.
/// <para>
/// These live in <c>Diaspora.Persistence</c> rather than in a module because that is where the
/// identity schema already is: <c>UserConfiguration</c>, <c>RefreshTokenConfiguration</c> and the rest
/// of the identity mappings are all here, and a provider that erases those tables belongs with them.
/// </para>
/// </summary>
public static class IdentityPrivacy
{
    /// <summary>The name Identity reports itself under, matching the module registry.</summary>
    public const string Module = "Identity";

    /// <summary>
    /// The country code the tombstone numbers sit under.
    /// <para>
    /// 999 is not assigned to any country by the ITU, so a placeholder cannot collide with a real
    /// number and cannot be dialled by anybody who finds one in a database.
    /// </para>
    /// </summary>
    private const string UnassignedCountryCode = "999";

    /// <summary>Builds the tombstone number for a given sequence value.</summary>
    public static PhoneNumber TombstoneNumber(long sequenceValue) =>
        PhoneNumber.Create(
            "+" + UnassignedCountryCode +
            sequenceValue.ToString("D11", CultureInfo.InvariantCulture));

    /// <summary>The name an erased account carries.</summary>
    public static FullName TombstoneName() => FullName.Create("Deleted member");
}

/// <summary>
/// Erases the member themselves: the account, its sessions, its devices, its codes and its history.
/// <para>
/// <b>The account row is anonymised rather than deleted, and everything hanging off it is deleted.</b>
/// The distinction matters and it is not arbitrary. The <c>users</c> row is what two hundred and
/// sixteen foreign keys across this platform point at, so removing it would either fail or cascade
/// into other members' content. Sessions, refresh tokens, devices, one-time codes and password history
/// are pointed at by nothing and are pure personal data, so they go.
/// </para>
/// <para>
/// <b>Security events are the deliberate exception.</b> They are the record of sign-ins, failures and
/// lockouts — the trail that answers "was this account compromised before it was closed" — and they
/// are keyed to a user id that is no longer attached to a person once the row above it is anonymised.
/// Keeping them is the same judgement the ledger makes, and the receipt says so rather than quietly
/// leaving them out.
/// </para>
/// </summary>
public sealed class IdentityDataErasureProvider : IDataErasureProvider
{
    private readonly DiasporaDbContext _dbContext;

    public IdentityDataErasureProvider(DiasporaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public string Module => IdentityPrivacy.Module;

    public async Task<IReadOnlyList<ErasureResult>> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken);

        if (user is null)
        {
            return [new ErasureResult(Module, "account", ErasureOutcome.NothingHeld, 0)];
        }

        var typed = UserId.From(userId);

        return
        [
            new ErasureResult(Module, "account", ErasureOutcome.Anonymized, 1),
            new ErasureResult(Module, "sessions", ErasureOutcome.Deleted,
                await _dbContext.Set<UserSession>().IgnoreQueryFilters().CountAsync(x => x.UserId == typed, cancellationToken)),
            new ErasureResult(Module, "devices", ErasureOutcome.Deleted,
                await _dbContext.Set<UserDevice>().IgnoreQueryFilters().CountAsync(x => x.UserId == typed, cancellationToken)),
            new ErasureResult(Module, "one-time codes", ErasureOutcome.Deleted,
                await _dbContext.Set<OtpChallenge>().IgnoreQueryFilters().CountAsync(x => x.UserId == typed, cancellationToken)),
            new ErasureResult(Module, "security events", ErasureOutcome.Retained,
                await _dbContext.Set<SecurityEvent>().IgnoreQueryFilters().CountAsync(x => x.UserId == typed, cancellationToken),
                "The sign-in and lockout trail is kept for security investigation. It is keyed to an "
                + "identifier that no longer names anybody once the account is anonymised."),
        ];
    }

    public async Task<IReadOnlyList<ErasureResult>> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(userId, cancellationToken, tracked: true);

        if (user is null)
        {
            return [new ErasureResult(Module, "account", ErasureOutcome.NothingHeld, 0)];
        }

        var typed = UserId.From(userId);
        var results = new List<ErasureResult>();

        // Sessions first: whatever else happens, the member must stop being signed in. Refresh tokens
        // are an owned collection of the session, so they go with it rather than needing their own pass.
        results.Add(new ErasureResult(Module, "sessions", ErasureOutcome.Deleted,
            await _dbContext.Set<UserSession>().IgnoreQueryFilters()
                .Where(x => x.UserId == typed).ExecuteDeleteAsync(cancellationToken)));

        results.Add(new ErasureResult(Module, "devices", ErasureOutcome.Deleted,
            await _dbContext.Set<UserDevice>().IgnoreQueryFilters()
                .Where(x => x.UserId == typed).ExecuteDeleteAsync(cancellationToken)));

        results.Add(new ErasureResult(Module, "one-time codes", ErasureOutcome.Deleted,
            await _dbContext.Set<OtpChallenge>().IgnoreQueryFilters()
                .Where(x => x.UserId == typed).ExecuteDeleteAsync(cancellationToken)));

        if (!user.IsAnonymized)
        {
            var sequenceValue = await NextTombstoneAsync(cancellationToken);

            var anonymized = user.Anonymize(
                IdentityPrivacy.TombstoneNumber(sequenceValue),
                IdentityPrivacy.TombstoneName(),
                DateTimeOffset.UtcNow);

            if (anonymized.IsFailure)
            {
                throw new InvalidOperationException(
                    $"The account could not be anonymised: {anonymized.Error.Code}.");
            }
        }

        results.Add(new ErasureResult(Module, "account", ErasureOutcome.Anonymized, 1));

        results.Add(new ErasureResult(Module, "security events", ErasureOutcome.Retained,
            await _dbContext.Set<SecurityEvent>().IgnoreQueryFilters()
                .CountAsync(x => x.UserId == typed, cancellationToken),
            "The sign-in and lockout trail is kept for security investigation. It is keyed to an "
            + "identifier that no longer names anybody once the account is anonymised."));

        return results;
    }

    /// <summary>
    /// Takes the next placeholder number from the sequence.
    /// <para>
    /// Raw SQL because reading a sequence is the one thing EF has no expression for, and the
    /// alternative — a counter in application memory — is exactly the race this sequence exists to
    /// close.
    /// </para>
    /// </summary>
    private async Task<long> NextTombstoneAsync(CancellationToken cancellationToken)
    {
        // Aliased to "Value" because that is the column name `SqlQueryRaw<T>` binds a scalar from.
        // Without it PostgreSQL returns a column called `nextval` and EF fails with
        // `42703: column s.Value does not exist` — in the middle of an erasure, which is the worst
        // place on this platform to discover a query shape is wrong.
        var sql =
            $"SELECT nextval('{DiasporaDbContext.IdentitySchema}.{DiasporaDbContext.AnonymizedPhoneSequence}') AS \"Value\"";

        return await _dbContext.Database
            .SqlQueryRaw<long>(sql)
            .SingleAsync(cancellationToken);
    }

    /// <summary>
    /// <c>IgnoreQueryFilters</c> because an erasure that is retried after a partial failure has to be
    /// able to find an account it already soft-deleted. Without it the second attempt would report
    /// <see cref="ErasureOutcome.NothingHeld"/> and leave the rest of the work undone.
    /// </summary>
    private Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = _dbContext.Set<User>().IgnoreQueryFilters().Where(user => user.Id == UserId.From(userId));

        return tracked
            ? query.FirstOrDefaultAsync(cancellationToken)
            : query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// The member's own account details, for their export.
/// <para>
/// <b>What they gave us, not what we concluded about them.</b> The profile, the devices they
/// registered and the sign-in trail are theirs. The password hash is not exported — it is a
/// credential, and putting one in a file a member downloads and forwards would be creating a risk in
/// the name of transparency. Neither is the security stamp, which is an implementation detail of token
/// invalidation and means nothing to anybody.
/// </para>
/// </summary>
public sealed class IdentityDataExportProvider : IDataExportProvider
{
    private readonly DiasporaDbContext _dbContext;

    public IdentityDataExportProvider(DiasporaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public string Module => IdentityPrivacy.Module;

    public async Task<IReadOnlyList<ExportSection>> ExportAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var typed = UserId.From(userId);

        var user = await _dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == typed, cancellationToken);

        if (user is null)
        {
            return [];
        }

        var account = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["userId"] = user.Id.Value,
            ["fullName"] = user.FullName.Value,
            ["phoneNumber"] = user.PhoneNumber.Value,
            ["email"] = user.Email?.Value,
            ["nationality"] = user.Nationality.Value,
            ["countryOfResidence"] = user.CountryOfResidence.Value,
            ["preferredLanguage"] = user.PreferredLanguage.Value,
            ["preferredTimeZone"] = user.PreferredTimeZone.Value,
            ["role"] = user.Role.Name,
            ["accessState"] = user.AccessState.Name,
            ["phoneVerifiedAt"] = user.Security.PhoneVerifiedAt,
            ["registeredAt"] = user.CreatedAt,
            ["lastLoginAt"] = user.LastLoginAt,
            ["lastActiveAt"] = user.LastActiveAt,
        };

        var devices = await _dbContext.Set<UserDevice>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(device => device.UserId == typed)
            .OrderBy(device => device.CreatedAt)
            .Select(device => new { device.Id, device.Platform, device.FriendlyName, device.CreatedAt, device.LastActiveAt })
            .ToListAsync(cancellationToken);

        var deviceRecords = devices
            .Select(device => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["deviceId"] = device.Id.Value,
                ["platform"] = device.Platform.ToString(),
                ["name"] = device.FriendlyName,
                ["registeredAt"] = device.CreatedAt,
                ["lastActiveAt"] = device.LastActiveAt,
            })
            .ToList<IReadOnlyDictionary<string, object?>>();

        // The sign-in trail. Theirs, and often the most useful thing in an export: it is how somebody
        // finds out that a device they do not recognise signed in three months ago.
        var security = await _dbContext.Set<SecurityEvent>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entry => entry.UserId == typed)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(500)
            .Select(entry => new { entry.Type, entry.OccurredAt, entry.IpAddress, entry.UserAgent })
            .ToListAsync(cancellationToken);

        var securityRecords = security
            .Select(entry => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = entry.Type.ToString(),
                ["occurredAt"] = entry.OccurredAt,
                ["ipAddress"] = entry.IpAddress,
                ["userAgent"] = entry.UserAgent,
            })
            .ToList<IReadOnlyDictionary<string, object?>>();

        return
        [
            new ExportSection(Module, "account", [account]),
            new ExportSection(Module, "devices", deviceRecords),
            new ExportSection(Module, "security events (most recent 500)", securityRecords),
        ];
    }
}
