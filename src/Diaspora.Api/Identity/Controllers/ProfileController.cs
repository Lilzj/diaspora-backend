using Asp.Versioning;
using Diaspora.Api.Common;
using Diaspora.Platform.Http;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Commands.Profile.ChangeAvatar;
using Diaspora.Application.Identity.Commands.Profile.UpdateProfile;
using Diaspora.Contracts.Identity.V1.Requests;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Modules.Media.Application.Assets;
using Diaspora.SharedKernel.Results;
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

    /// <summary>Makes one of the member's own image uploads their profile photo.</summary>
    /// <remarks>
    /// <para>
    /// Upload first (<c>POST /api/v1/media/uploads/direct</c>), then name the upload here. Media
    /// checks that it is the caller's own image, claims it for their profile and makes it public;
    /// only then does Identity record it, so a refused upload never touches the profile. The photo
    /// it replaces is made private again.
    /// </para>
    /// <para>
    /// The response's <c>avatarUrl</c> is absolute and unsigned — the same route listing
    /// photographs load from — so the app renders it directly.
    /// </para>
    /// </remarks>
    [HttpPut("avatar")]
    [Authorize(Policy = DiasporaPolicies.UpdateOwnProfile)]
    [ProducesResponseType<ApiResponse<AvatarResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAvatarAsync(
        [FromBody] SetAvatarRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var published = await _sender.Send(
            new PublishProfileImageCommand { MediaId = request.MediaId, Role = ProfileImageRoles.Avatar },
            cancellationToken);

        if (published.IsFailure)
        {
            return HandleResult(Result.Failure<AvatarResponse>(published.Error));
        }

        return await ChangeAvatarAsync(request.MediaId, cancellationToken);
    }

    /// <summary>Removes the member's profile photo and makes it private again.</summary>
    [HttpDelete("avatar")]
    [Authorize(Policy = DiasporaPolicies.UpdateOwnProfile)]
    [ProducesResponseType<ApiResponse<AvatarResponse>>(StatusCodes.Status200OK)]
    public Task<IActionResult> RemoveAvatarAsync(CancellationToken cancellationToken) =>
        ChangeAvatarAsync(null, cancellationToken);

    private async Task<IActionResult> ChangeAvatarAsync(Guid? mediaId, CancellationToken cancellationToken)
    {
        var changed = await _sender.Send(new ChangeAvatarCommand { MediaId = mediaId }, cancellationToken);

        if (changed.IsFailure)
        {
            return HandleResult(Result.Failure<AvatarResponse>(changed.Error));
        }

        if (changed.Value.PreviousMediaId is { } previous)
        {
            // Never fails the change: withdrawing is a no-op for anything that is not still this
            // member's public avatar, and the new photo is already recorded.
            await _sender.Send(
                new WithdrawProfileImageCommand { MediaId = previous, Role = ProfileImageRoles.Avatar },
                cancellationToken);
        }

        return HandleResult(Result.Success(new AvatarResponse
        {
            AvatarUrl = changed.Value.AvatarUrl,
            MediaId = changed.Value.MediaId
        }));
    }
}
