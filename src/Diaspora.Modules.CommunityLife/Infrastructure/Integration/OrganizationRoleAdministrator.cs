using Diaspora.Modules.CommunityLife.Application.Memberships;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.SharedKernel.Results;
using MediatR;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Integration;

/// <summary>
/// B12's role write, exposed as a port.
/// <para>
/// <b>A service rather than a handler, and that distinction is the whole reason this class exists.</b>
/// <c>DSP1001</c> forbids a handler depending on another handler, because a handler that can reach any
/// other handler can reach one whose authorization it has not performed — and the call site does not
/// look like it did anything. A service that dispatches a command is a different thing: it is this
/// module's own command, going through this module's own pipeline, with this module's own
/// authorization, validation and transaction behaviours applied.
/// </para>
/// <para>
/// <b>Nothing is reimplemented here.</b> The last-owner rule, the outranking rule, the history entry
/// and the domain event are all B12's handler's, and this class adds a translation and no policy.
/// </para>
/// </summary>
public sealed class OrganizationRoleAdministrator : IOrganizationRoleAdministrator
{
    private readonly ISender _sender;

    public OrganizationRoleAdministrator(ISender sender) => _sender = sender;

    public async Task<Result> ChangeRoleAsync(
        Guid organizationId,
        Guid membershipId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var changed = await _sender.Send(
            new ChangeMemberRoleCommand
            {
                OrganizationId = organizationId,
                MembershipId = membershipId,
                Request = new ChangeMemberRoleRequest { Role = role }
            },
            cancellationToken);

        return changed.IsFailure ? Result.Failure(changed.Error) : Result.Success();
    }
}
