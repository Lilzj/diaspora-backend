using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Financial;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Financial.Application.Memberships;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;


namespace Diaspora.Modules.CommunityProgramme.Application.Memberships;

/// <summary>
/// Records that a settled B15 payment paid for this membership. COMMUNITY-LIFE-GAP-003.
/// <para>
/// <b>The route is B18's and the financial decision is B15's, and that split is the whole design.</b>
/// It is the same shape as this module's organization-role write: the client holds a membership
/// identifier, so the route is keyed by one; the question "did this payment settle, was it for this
/// membership, and was it this person's" is B15's alone, and it is asked through <b>B15's own
/// binder service</b> rather than by re-deciding it here. Two modules that can both conclude a payment
/// settled is one too many, and the second is the one that gets it wrong.
/// </para>
/// <para>
/// <b>This module's check is additional rather than substitute.</b> The membership is loaded and its
/// holder checked before B15 is asked anything, so a caller cannot use the route to probe which
/// membership identifiers exist. B15 then checks ownership independently through the seam. A caller
/// who gets past the first has not got past the second.
/// </para>
/// <para>
/// <b>Activation happens here and again on the event, deliberately.</b> The synchronous activation
/// is what lets the client render an active membership on the screen the member is looking at; the
/// integration event is what repairs it when this second write fails. B15 commits the binding in its
/// own transaction, so a crash between the two leaves money captured and a membership short of it —
/// and that is precisely the case the reconciliation sweep re-announces. Both paths go through the
/// same <c>MembershipActivation</c> helper, and both are idempotent.
/// </para>
/// <para>
/// <b>B15's decision arrives through an injected service, not through <c>ISender</c>.</b> The
/// platform's <c>DSP1001</c> refuses a handler that can reach any other handler in the process, and
/// its own remedy is the one taken here: extract the shared behaviour into a service and inject it.
/// <c>MembershipPaymentBinder</c> is that service, it lives in B15, and it is the only thing on this
/// platform that decides whether a payment bought a membership.
/// </para>
/// <para>
/// <b>No <c>[Transactional]</c>.</b> The binder saves its own write; this handler's own write is a
/// single <c>SaveChanges</c> after it. Wrapping both would open one scope around two modules' writes
/// and make a membership activation able to roll back a financial record.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.Participate)]
[Auditable(Action = "community.membership.payment.bind")]
public sealed record RecordMembershipSettlementCommand : ICommand<ProgrammeMembershipResponse>
{
    public required Guid MembershipId { get; init; }

    public required BindMembershipPaymentRequest Request { get; init; }
}

public sealed class RecordMembershipSettlementCommandValidator
    : AbstractValidator<RecordMembershipSettlementCommand>
{
    public RecordMembershipSettlementCommandValidator()
    {
        RuleFor(command => command.MembershipId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.PaymentIntentId).NotEmpty();
    }
}

public sealed class RecordMembershipSettlementCommandHandler
    : ICommandHandler<RecordMembershipSettlementCommand, ProgrammeMembershipResponse>
{
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IMembershipProgrammeRepository _programmes;
    private readonly CommunityProgrammeAccessService _access;
    private readonly MembershipPaymentBinder _binder;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RecordMembershipSettlementCommandHandler(
        IProgrammeMembershipRepository memberships,
        IMembershipProgrammeRepository programmes,
        CommunityProgrammeAccessService access,
        MembershipPaymentBinder binder,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _memberships = memberships;
        _programmes = programmes;
        _access = access;
        _binder = binder;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProgrammeMembershipResponse>> Handle(
        RecordMembershipSettlementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(caller.Error);
        }

        var membership = await _memberships.GetAsync(
            ProgrammeMembershipId.From(command.MembershipId), cancellationToken);

        // Absent and somebody else's are answered identically, so the route cannot be used to
        // enumerate membership identifiers.
        if (membership is null || membership.MemberUserId != caller.Value)
        {
            return Result.Failure<ProgrammeMembershipResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        // B15's decision, passed through verbatim on refusal. Restating it in this module's
        // vocabulary would mean two catalogues describing one rule, and the client already handles
        // B15's financial errors from the checkout it just came through.
        var bound = await _binder.BindAsync(
            command.MembershipId, command.Request.PaymentIntentId, cancellationToken);

        if (bound.IsFailure)
        {
            return Result.Failure<ProgrammeMembershipResponse>(bound.Error);
        }

        var now = _clock.UtcNow;

        var programme = await _programmes.FindByOrganizationAsync(
            membership.OrganizationId, cancellationToken);

        var tier = programme?.FindTier(membership.TierId);

        // Only the request that actually created the binding activates. A repeat — a mobile retry, or
        // the loser of two simultaneous binds — is answered with the membership as it now stands.
        // Activating anyway would write against a revision the winner has already moved, and the
        // caller would be told their payment conflicted when it had in fact succeeded.
        if (bound.Value.IsNewBinding
            && MembershipActivation.Activate(
                membership, tier?.RenewalMonths, () => _guids.NewSequentialGuid(), now))
        {
            membership.AttachPayment(command.Request.PaymentIntentId, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Read rather than reused, and read-only rather than tracked: GetAsync resolves through the
        // identity map and would hand back the copy this request loaded before B15 committed — so a
        // "reload" would reload nothing, and the response would report a status that is no longer
        // true.
        var current = await _memberships.ReadAsync(
            ProgrammeMembershipId.From(command.MembershipId), cancellationToken) ?? membership;

        var organization = await _access.FindOrganizationAsync(
            current.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(current, tier, organization));
    }
}
