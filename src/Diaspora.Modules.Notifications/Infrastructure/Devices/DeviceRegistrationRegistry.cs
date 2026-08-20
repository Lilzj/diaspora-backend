using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Extensibility.Addressing;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Devices;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Devices;

/// <summary>
/// Resolves delivery endpoints from this module's own registrations, and from the address resolver
/// for channels that have none.
/// <para>
/// <b>Two sources behind one port, and the split is the ownership boundary.</b> Push endpoints are
/// registrations this module owns — a token has a lifecycle, a failure count and a retirement, and
/// all of that belongs where the failures happen. Email and phone addresses are Identity's, reached
/// through <see cref="IRecipientAddressResolver"/>, which today resolves nothing on purpose.
/// </para>
/// <para>
/// The dispatcher asks this and never the repository, so a deployment that moves push subscription
/// into a provider's topic service replaces this class and changes nothing above it.
/// </para>
/// </summary>
public sealed class DeviceRegistrationRegistry : IDeviceRegistry
{
    private readonly IDeviceRegistrationRepository _registrations;
    private readonly IRecipientAddressResolver _addresses;
    private readonly IClock _clock;

    public DeviceRegistrationRegistry(
        IDeviceRegistrationRepository registrations,
        IRecipientAddressResolver addresses,
        IClock clock)
    {
        _registrations = registrations;
        _addresses = addresses;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ChannelEndpoint>> ResolveAsync(
        UserId userId,
        NotificationChannelKind channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // The inbox addresses itself. Asking for endpoints would be a query per notification for an
        // answer that is always empty.
        if (channel == NotificationChannelKind.InApp)
        {
            return [];
        }

        var registered = await _registrations.ListDeliverableAsync(userId, channel, cancellationToken);

        if (registered.Count > 0)
        {
            return registered
                .Select(registration => new ChannelEndpoint
                {
                    EndpointId = registration.Id.Value,
                    Address = registration.Token,
                    Platform = registration.Platform.ToString()
                })
                .ToList();
        }

        return await _addresses.ResolveAsync(userId, channel, cancellationToken);
    }

    /// <summary>
    /// Retires endpoints a provider rejected.
    /// <para>
    /// Called after a send, including a successful one. Endpoints with no registration behind them —
    /// an address from the resolver — carry no identifier and are skipped rather than guessed at.
    /// </para>
    /// </summary>
    public async Task RetireAsync(
        IReadOnlyList<Guid> endpointIds,
        string reasonCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointIds);

        var now = _clock.UtcNow;

        foreach (var endpointId in endpointIds)
        {
            var registration = await _registrations.GetAsync(
                DeviceRegistrationId.From(endpointId), cancellationToken);

            if (registration is null)
            {
                continue;
            }

            registration.RecordFailure(reasonCode, isPermanent: true, now);
            _registrations.Update(registration);
        }
    }

    public async Task RecordSuccessAsync(
        IReadOnlyList<Guid> endpointIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpointIds);

        var now = _clock.UtcNow;

        foreach (var endpointId in endpointIds)
        {
            var registration = await _registrations.GetAsync(
                DeviceRegistrationId.From(endpointId), cancellationToken);

            if (registration is null)
            {
                continue;
            }

            registration.RecordDelivery(now);
            _registrations.Update(registration);
        }
    }
}
