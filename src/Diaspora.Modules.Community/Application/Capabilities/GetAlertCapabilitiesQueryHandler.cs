using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Capabilities;

/// <summary>
/// Answers "what may I publish" from the caller's own permissions.
/// <para>
/// The reasons name the requirement rather than guessing the cause. Permissions say what a caller
/// holds, not why they lack something — an unverified account and a restricted one look the same
/// from here — so a reason that said "you are restricted" would sometimes be false.
/// </para>
/// </summary>
public sealed class GetAlertCapabilitiesQueryHandler
    : IQueryHandler<GetAlertCapabilitiesQuery, AlertCapabilitiesResponse>
{
    private readonly ICurrentUser _currentUser;

    public GetAlertCapabilitiesQueryHandler(ICurrentUser currentUser) => _currentUser = currentUser;

    public Task<Result<AlertCapabilitiesResponse>> Handle(
        GetAlertCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var canPublish = Holds(CommunityPermissions.Post);
        var canPublishEmergency = canPublish && Holds(CommunityPermissions.PublishEmergency);

        UrgencyLevel[] levels = canPublishEmergency
            ? [UrgencyLevel.Low, UrgencyLevel.Normal, UrgencyLevel.Urgent, UrgencyLevel.Emergency]
            : [UrgencyLevel.Low, UrgencyLevel.Normal, UrgencyLevel.Urgent];

        return Task.FromResult(Result.Success(new AlertCapabilitiesResponse
        {
            CanPublish = canPublish,
            CannotPublishReason = canPublish
                ? null
                : "Publishing alerts needs a verified account in good standing.",
            CanPublishEmergency = canPublishEmergency,
            CannotPublishEmergencyReason = canPublishEmergency
                ? null
                : "Only verified members can publish an Emergency alert.",
            UrgencyLevels = canPublish ? levels.Select(level => CommunityWireNames.ToWire(level)).ToList() : [],
            MinAffectedRadiusMetres = CommunityLimits.MinAffectedRadiusMetres,
            MaxAffectedRadiusMetres = CommunityLimits.MaxAffectedRadiusMetres
        }));
    }

    private bool Holds(string permission) =>
        _currentUser.Permissions.Contains(permission, StringComparer.Ordinal);
}
