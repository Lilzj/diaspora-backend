using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Devices;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.Users;
using Mapster;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Registers the identity projections with Mapster, so <c>IMapper.Map&lt;T&gt;</c> works
/// for identity types alongside the rest of the platform.
/// <para>
/// Every registration delegates to an explicit mapper via <c>MapWith</c> rather than
/// relying on convention matching. Masking and omission are the point of these
/// projections; a convention mapper would silently undo them.
/// </para>
/// </summary>
public sealed class IdentityMappingRegistration : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.NewConfig<User, UserProfileResponse>()
            .MapWith(user => UserMapper.ToProfileResponse(user));

        config.NewConfig<UserDevice, DeviceResponse>()
            .MapWith(device => DeviceMapper.ToResponse(device));

        config.NewConfig<SecurityEvent, SecurityEventResponse>()
            .MapWith(securityEvent => SecurityEventMapper.ToResponse(securityEvent));

        config.NewConfig<AccessDecision, AccessDecisionResponse>()
            .MapWith(decision => AccessDecisionMapper.ToResponse(decision));

        config.NewConfig<AccessDecision, FeatureAccessResponse>()
            .MapWith(decision => AccessDecisionMapper.ToFeatureResponse(decision));
    }
}
