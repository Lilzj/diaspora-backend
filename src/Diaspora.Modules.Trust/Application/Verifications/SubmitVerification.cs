using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Verifications;

/// <summary>
/// Opens a verification request and hands it to review.
/// <para>
/// One command for every verification type. The type-specific parts — what evidence is
/// acceptable, whether a rule can decide it, how long the grant lasts — come from the
/// <see cref="IVerificationTypeHandler"/> registered for the type, so adding a type does not
/// touch this handler.
/// </para>
/// <para>
/// <c>[Transactional]</c> because a submission writes three aggregates: the request, the
/// member's profile (created on first contact) and their timeline. A partial write would
/// leave a member with a timeline entry for a verification that does not exist.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.SubmitVerification)]
[Transactional]
public sealed record SubmitVerificationCommand : ICommand<VerificationResponse>
{
    public required VerificationType Type { get; init; }

    public string? Nationality { get; init; }

    public string? ResidenceCountry { get; init; }

    public string? TargetCountry { get; init; }

    public DateOnly? ArrivalDate { get; init; }

    public string? SupportingNotes { get; init; }

    public string? SelfieReference { get; init; }

    public IReadOnlyList<string>? ExpertiseAreas { get; init; }

    public IReadOnlyList<DocumentSubmission> Documents { get; init; } = [];
}

/// <summary>One document being submitted with the request.</summary>
public sealed record DocumentSubmission
{
    public required VerificationDocumentType DocumentType { get; init; }

    public required string StorageKey { get; init; }

    public string? DocumentNumber { get; init; }

    public string? IssuingCountry { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    public string? FileName { get; init; }

    public string? ContentType { get; init; }

    public long SizeInBytes { get; init; }
}

public sealed class SubmitVerificationCommandValidator : AbstractValidator<SubmitVerificationCommand>
{
    public SubmitVerificationCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        // The latest calendar date anywhere (UTC+14). A member whose day has already begun is
        // never told their document was issued "in the future" because the server is in UTC.
        var latestToday = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddHours(14));

        RuleFor(command => command.Type).IsInEnum();

        RuleFor(command => command.Nationality)
            .Length(2).When(command => !string.IsNullOrWhiteSpace(command.Nationality))
            .WithMessage("Nationality must be a two-letter ISO country code.");

        RuleFor(command => command.ResidenceCountry)
            .Length(2).When(command => !string.IsNullOrWhiteSpace(command.ResidenceCountry))
            .WithMessage("Residence country must be a two-letter ISO country code.");

        RuleFor(command => command.TargetCountry)
            .Length(2).When(command => !string.IsNullOrWhiteSpace(command.TargetCountry))
            .WithMessage("Target country must be a two-letter ISO country code.");

        RuleFor(command => command.SupportingNotes)
            .MaximumLength(VerificationRequest.MaxSupportingNotesLength);

        RuleFor(command => command.ExpertiseAreas)
            .Must(areas => areas is null || areas.Count <= VerificationRequest.MaxExpertiseAreas)
            .WithMessage($"At most {VerificationRequest.MaxExpertiseAreas} expertise areas.");

        RuleForEach(command => command.Documents).ChildRules(document =>
        {
            document.RuleFor(item => item.DocumentType).IsInEnum();

            document.RuleFor(item => item.StorageKey)
                .NotEmpty()
                .MaximumLength(VerificationDocument.MaxStorageKeyLength);

            document.RuleFor(item => item.SizeInBytes)
                .LessThanOrEqualTo(VerificationDocument.MaxSizeInBytes)
                .WithMessage("Documents must be 5 MB or smaller.");

            document.RuleFor(item => item.DocumentNumber)
                .MaximumLength(VerificationDocument.MaxDocumentNumberLength);

            // Added 7 Oct 2026: a passport "issued" three weeks from now was accepted and
            // approved. A document cannot have been issued after today.
            document.RuleFor(item => item.IssuedOn)
                .Must(issued => issued is null || issued.Value <= latestToday)
                .WithMessage("The issue date cannot be in the future.");

            document.RuleFor(item => item.ExpiresOn)
                .Must((item, expires) => expires is null || item.IssuedOn is null || expires.Value > item.IssuedOn.Value)
                .WithMessage("The expiry date must be after the issue date.");
        });
    }
}

/// <summary>
/// The member's own submission, from their own screen.
/// <para>
/// <b>B3.0.2 moved the workflow into <see cref="VerificationSubmissionCoordinator"/>; this
/// handler kept the policy.</b> What it decides is what a duplicate means for a person clicking
/// submit twice — a conflict they should be told about — and that is different from what it
/// means for a module retrying after a timeout. The steps in between are identical for both and
/// now live in one place, which is the point.
/// </para>
/// </summary>
public sealed class SubmitVerificationCommandHandler
    : ICommandHandler<SubmitVerificationCommand, VerificationResponse>
{
    private readonly VerificationSubmissionCoordinator _submissions;
    private readonly ICurrentUser _currentUser;

    public SubmitVerificationCommandHandler(
        VerificationSubmissionCoordinator submissions, ICurrentUser currentUser)
    {
        _submissions = submissions;
        _currentUser = currentUser;
    }

    public async Task<Result<VerificationResponse>> Handle(
        SubmitVerificationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.NotAuthenticated);
        }

        var input = new VerificationSubmissionInput
        {
            UserId = userId,
            Type = request.Type,
            Nationality = request.Nationality,
            ResidenceCountry = request.ResidenceCountry,
            TargetCountry = request.TargetCountry,
            ArrivalDate = request.ArrivalDate,
            SupportingNotes = request.SupportingNotes,
            SelfieReference = request.SelfieReference,
            ExpertiseAreas = request.ExpertiseAreas,
            Documents = request.Documents

            // No subject: the member is verifying themselves, which is what this route has
            // always meant and what every row it has ever written records.
        };

        // One open request per type. Two live submissions is two reviewers doing the same
        // work and reaching possibly different answers.
        var existing = await _submissions.FindDuplicateAsync(input, cancellationToken);
        if (existing is not null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationDuplicate);
        }

        var submitted = await _submissions.SubmitAsync(input, cancellationToken);

        return submitted.IsFailure
            ? Result.Failure<VerificationResponse>(submitted.Error)
            : Result.Success(submitted.Value.ToResponse());
    }
}
