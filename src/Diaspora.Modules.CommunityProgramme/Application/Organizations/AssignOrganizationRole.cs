using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.CommunityProgramme.Application.Organizations;

/// <summary>
/// Assigns an organization role to a member.
/// <para>
/// <b>The route is B18's and the write is B12's, and that split is the whole design.</b> The register
/// proposes <c>POST /communities/organizations/{id}/members/{userId}/role</c> — keyed by the member,
/// which is what a client holding a member roll has. B12's own route is keyed by its membership row,
/// which the client does not have. So this handler resolves the one into the other through B12's
/// published directory and then dispatches <b>B12's own command</b>, which makes B12's handler decide
/// whether the caller may grant the role and B12 the only writer of one.
/// </para>
/// <para>
/// <b>Why not write it here.</b> Two modules that can both promote somebody is two places the
/// last-owner rule has to be enforced, and the second one is the one that forgets. B12 refuses to let
/// an owner be stood down, refuses a role above the caller's own, and raises the domain event its own
/// consumers listen for; every one of those would have to be reimplemented, and each reimplementation
/// is a place they diverge.
/// </para>
/// <para>
/// <b>The capability gate here is additional rather than substitute.</b> This module refuses without
/// <c>canManageMembers</c> before dispatching, so the failure a caller sees is B18's vocabulary; B12
/// then refuses independently on its own rules. A caller who gets past the first has not got past the
/// second.
/// </para>
/// <para>
/// <b>No <c>[Transactional]</c>, deliberately.</b> This handler stages nothing. B12's command carries
/// its own transactional declaration and its own save; adding one here would open a second scope
/// around a write this module does not own.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.ManageProgramme)]
public sealed record AssignOrganizationRoleCommand : ICommand<OrganizationCapabilitiesResponse>
{
    public required Guid OrganizationId { get; init; }

    public required Guid UserId { get; init; }

    public required AssignOrganizationRoleRequest Request { get; init; }
}

public sealed class AssignOrganizationRoleCommandValidator
    : AbstractValidator<AssignOrganizationRoleCommand>
{
    public AssignOrganizationRoleCommandValidator()
    {
        RuleFor(command => command.OrganizationId).NotEmpty();
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Role).NotEmpty().MaximumLength(32);
    }
}

public sealed class AssignOrganizationRoleCommandHandler
    : ICommandHandler<AssignOrganizationRoleCommand, OrganizationCapabilitiesResponse>
{
    private readonly CommunityProgrammeAccessService _access;
    private readonly IOrganizationRoleAdministrator _roles;

    public AssignOrganizationRoleCommandHandler(
        CommunityProgrammeAccessService access, IOrganizationRoleAdministrator roles)
    {
        _access = access;
        _roles = roles;
    }

    public async Task<Result<OrganizationCapabilitiesResponse>> Handle(
        AssignOrganizationRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationCapabilitiesResponse>(caller.Error);
        }

        var permitted = await _access.RequireCapabilityAsync(
            command.OrganizationId, OrganizationCapability.ManageMembers, cancellationToken);

        if (permitted.IsFailure)
        {
            return Result.Failure<OrganizationCapabilitiesResponse>(permitted.Error);
        }

        // The member's B12 row. Absent means they hold no current membership of this body, and a
        // role cannot be granted to somebody who has not joined — B12's own invitation route is how
        // that starts, and it is deliberately not fronted here.
        var membership = await _access.FindOrganizationMembershipAsync(
            command.OrganizationId, command.UserId, cancellationToken);

        if (membership is null)
        {
            return Result.Failure<OrganizationCapabilitiesResponse>(
                CommunityProgrammeErrors.MembershipNotFound);
        }

        var changed = await _roles.ChangeRoleAsync(
            command.OrganizationId,
            membership.MembershipId,
            command.Request.Role,
            cancellationToken);

        if (changed.IsFailure)
        {
            // B12's refusal, passed through verbatim. Restating it in this module's vocabulary would
            // mean two error catalogues describing one rule, and the client already handles B12's.
            return Result.Failure<OrganizationCapabilitiesResponse>(changed.Error);
        }

        return Result.Success(Mapping.CommunityProgrammeMapper.ToResponse(
            OrganizationCapabilityMap.For(command.OrganizationId, command.Request.Role)));
    }
}
