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

    public UpdateProfileCommandHandler(
        IUserRepository users,
        ICurrentIdentityContext currentIdentity,
        ISecurityEventWriter securityEvents,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _currentIdentity = currentIdentity;
        _securityEvents = securityEvents;
        _unitOfWork = unitOfWork;
        _clock = clock;
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

            // Uniqueness matters even though email is not yet a sign-in credential: it
            // will be, and letting duplicates accumulate now would make that migration
            // a data-cleanup exercise.
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

        return Result.Success(UserMapper.ToProfileResponse(user));
    }
}
