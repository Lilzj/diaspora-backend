using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Identity;

/// <summary>
/// Somebody joined.
/// <para>
/// <b>Two things happen, and only one of them is a message.</b> The welcome is an ordinary trigger.
/// The other is the one place this module writes a preference record on somebody's behalf: the
/// registration event carries a preferred language, and capturing it now is the difference between a
/// member's first notification arriving in their language and arriving in English with a settings
/// screen they have not found yet.
/// </para>
/// <para>
/// The record is created only if none exists, so a replayed event — which at-least-once delivery
/// guarantees — cannot overwrite a language the member has since chosen for themselves.
/// </para>
/// </summary>
public sealed class UserRegisteredConsumer : ModuleEventConsumer<UserRegisteredIntegrationEvent>
{
    private readonly INotificationPreferenceRepository _preferences;

    public UserRegisteredConsumer(
        INotificationDispatcher dispatcher, INotificationPreferenceRepository preferences)
        : base(dispatcher) => _preferences = preferences;

    protected override string SourceModule => "Identity";

    protected override IEnumerable<NotificationTrigger> Translate(
        UserRegisteredIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["fullName"] = integrationEvent.FullName;
        values["countryOfResidence"] = integrationEvent.CountryOfResidence;
        values["nationality"] = integrationEvent.Nationality;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.OccurredAt,
            values,
            subjectType: "account",
            subjectId: integrationEvent.UserId.ToString());
    }

    protected override async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await SeedPreferenceAsync(integrationEvent, cancellationToken);
        await base.HandleAsync(integrationEvent, cancellationToken);
    }

    private async Task SeedPreferenceAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var userId = UserId.From(integrationEvent.UserId);

        if (await _preferences.FindAsync(userId, cancellationToken) is not null)
        {
            return;
        }

        var preference = NotificationPreference.CreateDefault(
            Domain.Identifiers.NotificationPreferenceId.From(Guid.NewGuid()),
            userId,
            integrationEvent.OccurredAt,
            integrationEvent.PreferredLanguage);

        // A language the runtime does not recognise leaves the default in place rather than failing
        // registration's downstream handler. The member can set it themselves, and will be told in
        // English until they do — which is far better than not being told.
        if (preference.SetLanguage(integrationEvent.PreferredLanguage).IsFailure)
        {
            _ = NotificationErrors.LanguageUnsupported;
        }

        await _preferences.AddAsync(preference, cancellationToken);
    }
}
