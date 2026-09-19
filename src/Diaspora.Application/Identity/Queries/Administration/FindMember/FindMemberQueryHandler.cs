using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Queries.Administration.FindMember;

/// <summary>
/// Resolves the term the way a person would: it looks like an identifier, so try that; it
/// has an @, so try an address; otherwise try it as a phone number.
/// </summary>
public sealed class FindMemberQueryHandler : IQueryHandler<FindMemberQuery, AdminMemberResponse>
{
    private readonly IUserRepository _users;

    public FindMemberQueryHandler(IUserRepository users) => _users = users;

    public async Task<Result<AdminMemberResponse>> Handle(
        FindMemberQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var term = request.Term?.Trim() ?? string.Empty;

        if (term.Length == 0)
        {
            return Result.Failure<AdminMemberResponse>(UserErrors.NotFound);
        }

        var user = await ResolveAsync(term, cancellationToken);

        // The same answer for "no such account" and "that is not a usable handle": staff
        // asking about somebody who is not here learn only that, which is all they need.
        return user is null
            ? Result.Failure<AdminMemberResponse>(UserErrors.NotFound)
            : Result.Success(AdminMemberMapper.ToResponse(user));
    }

    private async Task<User?> ResolveAsync(string term, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(term, out var id))
        {
            return await _users.GetByIdAsync(UserId.From(id), cancellationToken);
        }

        if (term.Contains('@', StringComparison.Ordinal))
        {
            return EmailAddress.TryCreate(term, out var email) && email is not null
                ? await _users.GetByEmailAsync(email, cancellationToken)
                : null;
        }

        return PhoneNumber.TryCreate(term, out var phone) && phone is not null
            ? await _users.GetByPhoneNumberAsync(phone, cancellationToken)
            : null;
    }
}
