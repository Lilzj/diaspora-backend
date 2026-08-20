using Diaspora.Application.Identity.Models;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Decides whether a candidate password is acceptable. Kept separate from the hasher so
/// strength rules can change without touching credential storage.
/// </summary>
public interface IPasswordPolicy
{
    Result Validate(string password, PasswordPolicyContext context);

    PasswordPolicyDescriptor Describe();
}
