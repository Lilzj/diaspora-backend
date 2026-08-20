using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>
/// Records where the caller has lived.
/// <para>
/// <b>One save, and therefore no transaction.</b> This class carried
/// <c>[Transactional(Reason = "Writes the experience and its expertise claims together.")]</c>
/// until B1.4's <c>DSP4004</c> pointed out that the attribute does nothing on a handler:
/// <c>TransactionBehavior</c> reads it off <c>TRequest</c>, which is the command. No transaction
/// was ever opened here, and the comment above the attribute had been asserting one since B4.
/// </para>
/// <para>
/// The attribute was removed rather than moved, because its premise had also stopped being true.
/// The expertise claims are an owned collection on <see cref="CountryExperience"/>, not a second
/// aggregate, so the single <c>SaveChangesAsync</c> below writes the residence and its claims in
/// one statement batch — which EF already wraps in a transaction of its own. Adding one on top
/// would hold a connection longer and change nothing else.
/// </para>
/// </summary>
public sealed class DeclareCountryExperienceCommandHandler
    : ICommandHandler<DeclareCountryExperienceCommand, CountryExperienceResponse>
{
    private readonly ICountryExperienceRepository _experiences;
    private readonly IVerifiedResidencyRepository _residencies;
    private readonly ICountryRepository _countries;
    private readonly CountryNameResolver _countryNames;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DeclareCountryExperienceCommandHandler(
        ICountryExperienceRepository experiences,
        IVerifiedResidencyRepository residencies,
        ICountryRepository countries,
        CountryNameResolver countryNames,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _experiences = experiences;
        _residencies = residencies;
        _countries = countries;
        _countryNames = countryNames;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CountryExperienceResponse>> Handle(
        DeclareCountryExperienceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CountryExperienceResponse>(LocationErrors.NotAuthenticated);
        }

        var payload = request.Experience;

        var code = CountryCode.TryCreate(payload.CountryCode);

        if (code.IsFailure)
        {
            return Result.Failure<CountryExperienceResponse>(code.Error);
        }

        // The country must be in the catalogue. Accepting a well-formed code the platform does
        // not know would create a residence in a country no screen can render.
        if (!await _countries.ExistsAsync(code.Value, cancellationToken))
        {
            return Result.Failure<CountryExperienceResponse>(
                LocationErrors.CountryNotFound(code.Value.Value));
        }

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var period = ResidencePeriod.TryCreate(payload.ArrivedOn, payload.DepartedOn, today);

        if (period.IsFailure)
        {
            return Result.Failure<CountryExperienceResponse>(period.Error);
        }

        var experience = await _experiences.GetAsync(userId, code.Value, cancellationToken);

        if (experience is null)
        {
            var declared = CountryExperience.Declare(
                userId, code.Value, period.Value, payload.Role);

            if (declared.IsFailure)
            {
                return Result.Failure<CountryExperienceResponse>(declared.Error);
            }

            experience = declared.Value;
            await _experiences.AddAsync(experience, cancellationToken);
        }
        else
        {
            var amended = experience.AmendPeriod(period.Value);

            if (amended.IsFailure)
            {
                return Result.Failure<CountryExperienceResponse>(amended.Error);
            }

            experience.SetRole(payload.Role);
        }

        experience.SetFamiliarity(
            ParseFamiliarity(payload.LanguageFamiliarity),
            ParseFamiliarity(payload.CulturalFamiliarity));

        if (payload.ExpertiseAreas is not null)
        {
            var replaced = experience.ReplaceExpertise(payload.ExpertiseAreas, now);

            if (replaced.IsFailure)
            {
                return Result.Failure<CountryExperienceResponse>(replaced.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var residency = await _residencies.GetAsync(userId, code.Value, cancellationToken);

        var state = residency is null
            ? ResidencyVerificationState.Unverified
            : residency.IsVerifiedAt(now)
                ? ResidencyVerificationState.Verified
                : residency.State;

        var countryName = await _countryNames.NameForAsync(code.Value, cancellationToken);

        return Result.Success(
            ExperienceMapper.ToResponse(experience, countryName, state, today));
    }

    /// <summary>
    /// Unrecognised familiarity falls back to <see cref="FamiliarityLevel.None"/> rather than
    /// throwing. It is a self-assessment with no consequence beyond ranking, and rejecting a
    /// whole residence over a typo in an optional field would be disproportionate.
    /// </summary>
    private static FamiliarityLevel ParseFamiliarity(string? value) =>
        Enum.TryParse<FamiliarityLevel>(value, ignoreCase: true, out var parsed)
            ? parsed
            : FamiliarityLevel.None;
}
