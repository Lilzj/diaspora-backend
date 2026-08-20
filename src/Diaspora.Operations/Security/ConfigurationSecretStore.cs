using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Security;

/// <summary>
/// The shipped <see cref="ISecretStore"/>: configuration underneath, with the version, expiry and
/// rotation vocabulary the platform needs on top.
/// <para>
/// <b>What changes on day one: nothing about where secrets live.</b> They are still read from
/// <c>IConfiguration</c>, which is still an environment variable or a mounted file. What changes is
/// that the platform can now say which secrets it expects, whether each is present and well-formed,
/// when each was last rotated and which are overdue — and can fail a startup over a missing one
/// instead of over the first request that needed it.
/// </para>
/// <para>
/// <b>A rotation performed here survives until the process stops.</b> It writes to an in-memory
/// overlay, logs at warning, and is reported on the dashboard as unpersisted. That is the honest
/// behaviour of a store with no writable backing, and it is stated in the log line so nobody discovers
/// it at the next deployment. A managed secret store replaces this class and nothing that reads a
/// secret changes.
/// </para>
/// </summary>
public sealed class ConfigurationSecretStore : ISecretStore
{
    private const string SectionName = "Secrets";

    private readonly ConcurrentDictionary<string, Rotated> _rotated = new(StringComparer.Ordinal);
    private readonly IReadOnlyList<SecretRequirement> _requirements;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ILogger<ConfigurationSecretStore> _logger;

    public ConfigurationSecretStore(
        IEnumerable<ISecretRequirementContributor> contributors,
        IConfiguration configuration,
        IClock clock,
        ILogger<ConfigurationSecretStore> logger)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _configuration = configuration;
        _clock = clock;
        _logger = logger;

        _requirements =
        [
            .. contributors
                .SelectMany(contributor => contributor.Secrets ?? [])
                .GroupBy(requirement => requirement.Name, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(requirement => requirement.Name, StringComparer.Ordinal)
        ];
    }

    public Task<SecretValue?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();

        if (_rotated.TryGetValue(name, out var rotated))
        {
            return Task.FromResult<SecretValue?>(new SecretValue(name, rotated.Version, rotated.Value));
        }

        var configured = _configuration[$"{SectionName}:{name}"];

        return Task.FromResult(
            string.IsNullOrEmpty(configured) ? null : new SecretValue(name, 1, configured));
    }

    public async Task<IReadOnlyList<SecretValue>> GetAcceptedAsync(
        string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var accepted = new List<SecretValue>(2);

        var active = await GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (active is not null)
        {
            accepted.Add(active);
        }

        // The superseded value, while its retirement window is open. This is the whole point of the
        // retiring state: a token signed under the previous key must keep verifying until it expires,
        // or a rotation signs every member out at once.
        if (_rotated.TryGetValue(name, out var rotated) &&
            rotated.Previous is { } previous &&
            _clock.UtcNow < rotated.PreviousRetiresAt)
        {
            accepted.Add(new SecretValue(name, rotated.Version - 1, previous));
        }

        return accepted;
    }

    public Task<IReadOnlyList<SecretDescriptor>> DescribeAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SecretDescriptor> descriptors =
        [
            .. _requirements
                .Select(requirement => Describe(requirement.Name, requirement))
                .OfType<SecretDescriptor>()
        ];

        return Task.FromResult(descriptors);
    }

    public Task<SecretDescriptor?> DescribeAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var requirement = _requirements.FirstOrDefault(
            candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        return Task.FromResult(Describe(name, requirement));
    }

    public Task<SecretDescriptor> RotateAsync(
        string name,
        string newValue,
        TimeSpan retirementWindow,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(newValue);
        ArgumentOutOfRangeException.ThrowIfNegative(retirementWindow.Ticks);

        var requirement = _requirements.FirstOrDefault(
            candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        if (requirement is { MinimumLength: > 0 } && newValue.Length < requirement.MinimumLength)
        {
            throw new ArgumentException(
                $"'{name}' requires at least " +
                $"{requirement.MinimumLength.ToString(CultureInfo.InvariantCulture)} characters.",
                nameof(newValue));
        }

        var previous = _rotated.TryGetValue(name, out var existing)
            ? existing.Value
            : _configuration[$"{SectionName}:{name}"];

        var version = (existing?.Version ?? 1) + 1;
        var now = _clock.UtcNow;

        _rotated[name] = new Rotated(newValue, version, now, previous, now + retirementWindow);

        OperationsTelemetry.SecretRotations.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, name));

        _logger.LogWarning(
            "Secret {Secret} was rotated to version {Version}. The previous version stays accepted " +
            "until {RetiresAt:O}. This store holds rotations in memory only — the new value is lost " +
            "when this process stops, and other instances are still on the old one. Promote it into " +
            "configuration before relying on it.",
            name, version, now + retirementWindow);

        return Task.FromResult(Describe(name, requirement)!);
    }

    public Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var problems = new List<string>();
        var now = _clock.UtcNow;

        foreach (var requirement in _requirements)
        {
            var value = _rotated.TryGetValue(requirement.Name, out var rotated)
                ? rotated.Value
                : _configuration[$"{SectionName}:{requirement.Name}"];

            if (string.IsNullOrWhiteSpace(value))
            {
                if (requirement.Required)
                {
                    problems.Add(
                        $"'{requirement.Name}' is required by {requirement.Module} and is not configured. " +
                        $"{requirement.Description}");
                }

                continue;
            }

            if (requirement.MinimumLength > 0 && value.Length < requirement.MinimumLength)
            {
                // The length, never the value. A validation message that quoted the secret would put
                // it in the startup log of every instance.
                problems.Add(
                    $"'{requirement.Name}' is " +
                    $"{value.Length.ToString(CultureInfo.InvariantCulture)} characters; " +
                    $"{requirement.Module} requires at least " +
                    $"{requirement.MinimumLength.ToString(CultureInfo.InvariantCulture)}.");
            }

            var descriptor = Describe(requirement.Name, requirement);

            if (descriptor is not null && descriptor.IsExpired(now))
            {
                problems.Add(
                    $"'{requirement.Name}' should have been rotated by {descriptor.ExpiresAt:O}.");
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(problems);
    }

    /// <summary>Secrets due for rotation within <paramref name="window"/>. Read by the health check.</summary>
    public async Task<IReadOnlyList<SecretDescriptor>> ExpiringWithinAsync(
        TimeSpan window, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var all = await DescribeAllAsync(cancellationToken).ConfigureAwait(false);

        return [.. all.Where(descriptor => descriptor.IsExpiringSoon(now, window) || descriptor.IsExpired(now))];
    }

    private SecretDescriptor? Describe(string name, SecretRequirement? requirement)
    {
        var rotated = _rotated.GetValueOrDefault(name);
        var value = rotated?.Value ?? _configuration[$"{SectionName}:{name}"];

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Without a rotation, the platform cannot know when a configured secret was created — the
        // configuration provider carries no timestamp. The process start is the earliest defensible
        // answer and it is the one that makes an un-rotated secret look old rather than new.
        var createdAt = rotated?.RotatedAt ?? ProcessStartedAt;

        return new SecretDescriptor(
            name,
            rotated?.Version ?? 1,
            SecretStatus.Active,
            createdAt,
            requirement?.RotateEvery is { } every ? createdAt + every : null,
            rotated?.RotatedAt,
            Fingerprint(value));
    }

    /// <summary>
    /// A short, salted-by-name hash of a value.
    /// <para>
    /// Twelve hex characters: enough for two operators to confirm that staging and production hold
    /// different keys, far too little to attack. Including the name means the same value under two
    /// names fingerprints differently, which stops the display itself becoming a way to discover that
    /// two secrets are the same.
    /// </para>
    /// </summary>
    private static string Fingerprint(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexStringLower(hash)[..12];
    }

    private static readonly DateTimeOffset ProcessStartedAt =
        DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);

    private sealed record Rotated(
        string Value,
        int Version,
        DateTimeOffset RotatedAt,
        string? Previous,
        DateTimeOffset PreviousRetiresAt);
}
