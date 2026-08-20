using System.Diagnostics;
using Diaspora.Modules.Conversations.Domain.Catalog;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Platform.Operations.Health;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Conversations.Infrastructure;

/// <summary>
/// What an operator needs to know about conversations without opening one.
/// <para>
/// <b>Three checks, and each answers a question that has previously been answered wrongly by
/// inference.</b> An empty context-type catalogue means every attempt to open a conversation is
/// refused with "unknown type" — which reads to a member as a broken button and to an operator as
/// nothing at all, because no exception is thrown. A missing realtime notifier means messages land
/// and nobody is told until they refresh. A missing participant directory means every name in every
/// conversation renders blank, which support will receive as "the app lost everyone's name".
/// </para>
/// <para>
/// <b>Degraded, not unhealthy, for the last two.</b> A deployment with no directory and no live
/// transport still delivers messages correctly; reporting it unhealthy would take a working
/// deployment out of a load balancer over a rendering concern.
/// </para>
/// </summary>
public sealed class ConversationHealthContributor : IModuleHealthContributor
{
    private readonly IConversationContextTypeRepository _types;
    private readonly IConversationParticipantDirectory _directory;
    private readonly IRealtimeNotifier _notifier;
    private readonly IOptionsMonitor<ConversationOptions> _options;

    public ConversationHealthContributor(
        IConversationContextTypeRepository types,
        IConversationParticipantDirectory directory,
        IRealtimeNotifier notifier,
        IOptionsMonitor<ConversationOptions> options)
    {
        _types = types;
        _directory = directory;
        _notifier = notifier;
        _options = options;
    }

    public string Module => ConversationsModuleMetadata.Name;

    public async Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var results = new List<ModuleHealthResult>(3);

        var stopwatch = Stopwatch.StartNew();

        var types = await _types.ListAsync(activeOnly: true, cancellationToken);

        results.Add(types.Count == 0
            ? ModuleHealthResult.Unhealthy(
                "conversation-context-types",
                Module,
                HealthDimension.Storage,
                "The conversation context type catalogue is empty. Every attempt to open a "
                + "conversation will be refused as an unknown type, silently, with no exception "
                + "anywhere. Check that the catalogue seeder ran.",
                new Dictionary<string, object> { ["elapsedMs"] = stopwatch.ElapsedMilliseconds })
            : ModuleHealthResult.Healthy(
                "conversation-context-types",
                Module,
                HealthDimension.Storage,
                $"{types.Count} conversation context types are active.",
                new Dictionary<string, object>
                {
                    ["count"] = types.Count,
                    ["elapsedMs"] = stopwatch.ElapsedMilliseconds
                }));

        results.Add(options.EnableRealtime && !_notifier.IsEnabled
            ? ModuleHealthResult.Degraded(
                "conversation-realtime",
                Module,
                HealthDimension.Messaging,
                "Conversation real-time is switched on for this module but the Communication "
                + "Platform's notifier is disabled, so no message reaches a live client. Messages "
                + "are still stored and still push.")
            : ModuleHealthResult.Healthy(
                "conversation-realtime",
                Module,
                HealthDimension.Messaging,
                options.EnableRealtime
                    ? "Conversation messages are relayed live."
                    : "Conversation real-time is switched off for this deployment.",
                new Dictionary<string, object>
                {
                    ["moduleEnabled"] = options.EnableRealtime,
                    ["transportEnabled"] = _notifier.IsEnabled
                }));

        results.Add(_directory.IsAvailable
            ? ModuleHealthResult.Healthy(
                "conversation-participants",
                Module,
                HealthDimension.Dependency,
                "A participant directory is registered; names and trust marks resolve.")
            : ModuleHealthResult.Degraded(
                "conversation-participants",
                Module,
                HealthDimension.Dependency,
                "No participant directory is registered, so display names, avatars and trust marks "
                + "are null on every conversation. This is the shipped default and messaging works "
                + "without it — register an IConversationParticipantDirectory to populate them."));

        return results;
    }
}
