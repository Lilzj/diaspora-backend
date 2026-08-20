using Diaspora.Modules.Notifications.Extensibility.Audiences;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Audiences;

/// <summary>
/// Resolves no audience, deliberately.
/// <para>
/// The honest implementation of a question this module cannot answer: nothing in the platform
/// publishes who lives in a country, and this module must not build a copy of that list to find out.
/// Broadcast rules exist, their templates are seeded and their delivery path is exercised — they
/// simply reach nobody until an adapter is registered. See
/// <see cref="INotificationAudienceResolver"/> for why that is the right shape.
/// </para>
/// </summary>
public sealed class EmptyNotificationAudienceResolver : INotificationAudienceResolver
{
    private static readonly IReadOnlyList<UserId> None = [];

    public Task<IReadOnlyList<UserId>> ResolveCountryAudienceAsync(
        string countryCode,
        string categoryKey,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(None);
}
