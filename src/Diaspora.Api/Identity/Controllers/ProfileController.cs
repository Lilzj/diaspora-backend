using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Profile.UpdateProfile;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diaspora.Api.Identity.Controllers;

/// <summary>
/// Member profile management. Also serves the "complete your profile" step that follows
/// phone verification.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route(ApiEndpoints.BaseUrl + "/auth/profile")]
[Produces("application/json")]
[Authorize]
public sealed class ProfileController : ApiController
{
    private readonly ISender _sender;

    public ProfileController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Updates the member-editable profile fields.</summary>
    /// <remarks>
    /// Phone number is not editable here. Changing it re-opens verification, so it is a
    /// separate flow rather than a field on this payload.
    /// </remarks>
    [HttpPut]
    [Authorize(Policy = DiasporaPolicies.UpdateOwnProfile)]
    [ProducesResponseType<ApiResponse<UserProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await _sender.Send(new UpdateProfileCommand
        {
            FullName = request.FullName,
            Nationality = request.Nationality,
            CountryOfResidence = request.CountryOfResidence,
            PreferredLanguage = request.PreferredLanguage,
            PreferredTimeZone = request.PreferredTimeZone,
            Email = request.Email,
            AvatarStorageKey = request.AvatarStorageKey
        }, cancellationToken);

        return HandleResult(result);
    }
}
