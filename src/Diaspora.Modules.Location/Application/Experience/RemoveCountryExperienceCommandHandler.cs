using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>Withdraws a declared residence.</summary>
public sealed class RemoveCountryExperienceCommandHandler
    : ICommandHandler<RemoveCountryExperienceCommand>
{
    private readonly ICountryExperienceRepository _experiences;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCountryExperienceCommandHandler(
        ICountryExperienceRepository experiences,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _experiences = experiences;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RemoveCountryExperienceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(LocationErrors.NotAuthenticated);
        }

        var code = CountryCode.TryCreate(request.CountryCode);

        if (code.IsFailure)
        {
            return Result.Failure(code.Error);
        }

        var experience = await _experiences.GetAsync(userId, code.Value, cancellationToken);

        if (experience is null)
        {
            return Result.Failure(LocationErrors.ExperienceNotFound);
        }

        _experiences.Remove(experience);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
