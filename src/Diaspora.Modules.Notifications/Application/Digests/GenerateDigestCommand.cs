using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Digests;

/// <summary>The member asked for a summary now.</summary>
[Authorize(Permission = NotificationPermissions.DigestsGenerate)]
[Transactional(Reason = "Assembles the digest and marks its notifications together.")]
public sealed record GenerateDigestCommand : ICommand<NotificationDigestResponse>
{
    public required GenerateDigestRequest Request { get; init; }
}
