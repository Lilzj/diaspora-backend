using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Commands.Profile.UpdateProfile;

public sealed class UpdateProfileCommandHandler : ICommandHandler<UpdateProfileCommand, UserProfileResponse>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly ISecurityEventWriter _securityEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAvatarUrlResolver _avatars;

    public UpdateProfileCommandHandler(
        IUserRepository users,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IAvatarUrlResolver avatars,
        IClock clock)
    {
        _users = users;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _avatars = avatars;
    }

    public async Task<Result<UserProfileResponse>> Handle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;
        if (userId is null)
        {
            return Result.Failure<UserProfileResponse>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserProfileResponse>(UserErrors.NotFound);
        }

        if (!FullName.TryCreate(request.FullName, out var fullName) || fullName is null ||
            !CountryCode.TryCreate(request.Nationality, out var nationality) || nationality is null ||
            !CountryCode.TryCreate(request.CountryOfResidence, out var residence) || residence is null ||
            !LanguageCode.TryCreate(request.PreferredLanguage, out var language) || language is null ||
            !TimeZoneIdentifier.TryCreate(request.PreferredTimeZone, out var timeZone) || timeZone is null)
        {
            return Result.Failure<UserProfileResponse>(Error.Validation(
                "Identity.Profile.Invalid", "One or more profile fields are invalid."));
        }

        var now = _clock.UtcNow;

        var update = user.UpdateProfile(fullName, nationality, residence, language, timeZone, now);
        if (update.IsFailure)
        {
            return Result.Failure<UserProfileResponse>(update.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            EmailAddress email;
            try
            {
                email = EmailAddress.Create(request.Email);
            }
            catch (ArgumentException)
            {
                return Result.Failure<UserProfileResponse>(Error.Validation(
                    nameof(request.Email), "Enter a valid email address."));
            }

            // The sign-in address does not move here. This route needs no password and no proof
            // the member receives mail at the new address, and until 20 Oct 2026 it moved the
            // address anyway — a stolen session was a stolen account — and answered 409 for an
            // address another account used, which made it an enumeration oracle. The change is
            // `PUT /auth/email` now. Refused before the uniqueness check, so the refusal says
            // nothing about the address. An account with no address may still add one here.
            if (user.Email is not null && user.Email != email)
            {
                return Result.Failure<UserProfileResponse>(Error.Validation(
                    "Identity.Profile.EmailChangeNeedsConfirmation",
                    "Change your email address from its own screen: it needs your password and a link sent to the new address."));
            }

            if (user.Email != email && await _users.ExistsByEmailAsync(email, cancellationToken))
            {
                return Result.Failure<UserProfileResponse>(UserErrors.EmailAlreadyRegistered);
            }

            user.SetEmail(email, now);
        }

        if (AvatarReference.TryCreate(request.AvatarStorageKey, out var avatar))
        {
            user.SetAvatar(avatar, now);
        }

        _users.Update(user);

        await _securityEvents.WriteAsync(new SecurityEventDraft
        {
            UserId = user.Id,
            SessionId = _currentIdentity.SessionId,
            Type = SecurityEventType.ProfileUpdated,
            Severity = SecurityEventSeverity.Informational,
            Description = "Profile updated."
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(UserMapper.ToProfileResponse(user, _avatars));
    }
}
