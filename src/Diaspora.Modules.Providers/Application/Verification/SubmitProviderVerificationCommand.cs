using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Verification;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Providers.Application.Verification;

/// <summary>
/// Raises a verification claim for a provider.
/// <para>
/// <b>Asks. It does not grant.</b> Everything this command can do is record that a claim went out
/// and store the identifier Trust will quote back. The badge arrives later, on Trust's integration
/// event, through <c>TrustVerificationProjector</c> — the only path to
/// <c>ApplyVerificationOutcome</c> and the reason a module cannot verify itself.
/// </para>
/// <para>
/// <b>Transactional, and the reason is the correlation identifier.</b> The claim row and Trust's
/// case must commit together: a provider row saved without the identifier is a claim nobody can
/// match a decision to, and a Trust case with no provider row is a review whose outcome lands
/// nowhere. Trust's own submission joins this transaction rather than opening its own — the
/// platform's unit of work is re-entrant precisely so a cross-module call composes this way.
/// </para>
/// </summary>
[RequirePermission(ProviderPermissions.SubmitVerification)]
[Transactional]
public sealed record SubmitProviderVerificationCommand : ICommand<ProviderVerificationClaimAccepted>
{
    public required Guid ProviderId { get; init; }

    public required SubmitProviderVerificationRequest Request { get; init; }
}

/// <summary>
/// What B7 knows the moment a claim is raised: that it was, and which case holds it.
/// <para>
/// Deliberately not a verification status. The claim is <c>Pending</c> by construction and saying
/// so in a response invites a client to render it as progress toward a badge, which it is not.
/// </para>
/// </summary>
public sealed record ProviderVerificationClaimAccepted
{
    public required Guid ProviderId { get; init; }

    /// <summary>The <c>ProviderVerificationKind</c> name.</summary>
    public required string Kind { get; init; }

    /// <summary>Trust's identifier for the case. What its decision events will quote back.</summary>
    public required Guid TrustVerificationId { get; init; }
}

public sealed class SubmitProviderVerificationCommandValidator
    : AbstractValidator<SubmitProviderVerificationCommand>
{
    public SubmitProviderVerificationCommandValidator()
    {
        RuleFor(command => command.ProviderId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.Kind)
            .NotEmpty()
            .When(command => command.Request is not null);

        RuleFor(command => command.Request.EvidenceMediaIds)
            .Must(ids => ids is null || ids.Count <= MaxEvidenceItems)
            .WithMessage($"At most {MaxEvidenceItems} pieces of evidence.")
            .When(command => command.Request is not null);
    }

    /// <summary>Trust caps a review at what a human can reasonably open in one sitting.</summary>
    public const int MaxEvidenceItems = 20;
}

public sealed class SubmitProviderVerificationCommandHandler
    : ICommandHandler<SubmitProviderVerificationCommand, ProviderVerificationClaimAccepted>
{
    private readonly IProviderRepository _providers;
    private readonly ProviderVerificationProviderRegistry _verifiers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SubmitProviderVerificationCommandHandler(
        IProviderRepository providers,
        ProviderVerificationProviderRegistry verifiers,
        ProviderAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _providers = providers;
        _verifiers = verifiers;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ProviderVerificationClaimAccepted>> Handle(
        SubmitProviderVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(ProviderErrors.NotFound);
        }

        // Ownership, not merely permission. The permission says this member may submit
        // verifications; this says they may submit one for *this* provider.
        var caller = _authorization.RequireRole(provider, ProviderRole.Manager);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(caller.Error);
        }

        var kind = ProviderWireNames.ParseVerificationKind(command.Request.Kind);

        if (kind.IsFailure)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(kind.Error);
        }

        if (!_verifiers.CanHandle(kind.Value))
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(
                ProviderErrors.VerificationProviderUnavailable);
        }

        var certificationId = command.Request.CertificationId is { } certification
            ? ProviderCertificationId.From(certification)
            : null;

        var submitted = await _verifiers.SubmitAsync(
            BuildRequest(provider, kind.Value, caller.Value.Value, command.Request),
            cancellationToken);

        if (submitted.IsFailure)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(submitted.Error);
        }

        // A verifier that decided without an identifier has nothing B7 can reconcile against.
        // Recording the claim anyway would produce a pending verification that no decision can
        // ever find — the failure mode the registry's own summary calls out.
        if (submitted.Value.ExternalVerificationId is not { } trustVerificationId)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(
                ProviderErrors.VerificationProviderUnavailable);
        }

        var recorded = provider.SubmitVerification(
            kind.Value,
            ProviderVerificationId.From(_guids.NewGuid()),
            trustVerificationId,
            caller.Value,
            _clock.UtcNow,
            certificationId);

        if (recorded.IsFailure)
        {
            return Result.Failure<ProviderVerificationClaimAccepted>(recorded.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        ProviderTelemetry.VerificationsSubmitted.Add(
            1, new KeyValuePair<string, object?>("kind", kind.Value.ToString()));

        return Result.Success(new ProviderVerificationClaimAccepted
        {
            ProviderId = provider.Id.Value,
            Kind = kind.Value.ToString(),
            TrustVerificationId = trustVerificationId
        });
    }

    /// <summary>
    /// Flattens the aggregate into what a verifier is told.
    /// <para>
    /// Flat primitives rather than the <c>Provider</c> itself, which is the seam's whole point:
    /// a verifier written against a national companies register needs a company number and a
    /// jurisdiction, and handing it the aggregate would let it mutate what it was asked to check.
    /// </para>
    /// </summary>
    private static ProviderVerificationRequest BuildRequest(
        Provider provider,
        ProviderVerificationKind kind,
        Guid submittedBy,
        SubmitProviderVerificationRequest request)
    {
        var certification = request.CertificationId is { } id
            ? provider.Certifications.FirstOrDefault(record => record.Id.Value == id)
            : null;

        // The registration details a reviewer checks against a register live on the optional
        // organisation block: a sole trader has none, and a claim about one is thinner evidence
        // rather than an error.
        var organization = provider.Organization;

        return new ProviderVerificationRequest
        {
            ProviderId = provider.Id.Value,
            Kind = kind,
            SubmittedBy = submittedBy,
            ProviderType = provider.Type,
            ProviderName = provider.Name.Value,
            LegalName = organization?.LegalName,
            RegistrationNumber = organization?.RegistrationNumber,
            RegistrationJurisdiction = organization?.RegistrationJurisdiction,
            TaxIdentifier = organization?.TaxIdentifier,
            CountryCode = provider.Location.CountryCode,
            CertificationId = request.CertificationId,
            CertificationName = certification?.Name,
            CertificationIssuer = certification?.Issuer,
            CertificationReference = certification?.Reference,
            EvidenceMediaIds = request.EvidenceMediaIds ?? []
        };
    }
}
