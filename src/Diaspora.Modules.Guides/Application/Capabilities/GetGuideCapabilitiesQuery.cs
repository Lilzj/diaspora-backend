using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Capabilities;

/// <summary>
/// What this deployment of B8 can do.
/// <para>
/// The call a client makes once at startup so its editor, filter sheet and client-side
/// validation come from the server rather than from a shipped constant. The block kinds the
/// editor may offer, the sort keys discovery accepts, the ranking strategies the installed
/// modules registered and the ceilings the aggregate enforces are all deployment facts today
/// baked into the app; this is what unbakes them.
/// </para>
/// <para>
/// Public and unauthenticated: a client needs it before anybody has signed in, and everything in
/// it is a description of the software rather than of anyone's data.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record GetGuideCapabilitiesQuery : IQuery<GuideCapabilitiesResponse>;
