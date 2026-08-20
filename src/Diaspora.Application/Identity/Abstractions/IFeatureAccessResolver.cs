using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Maps a position on the progressive access ladder onto the set of open feature gates.
/// </summary>
public interface IFeatureAccessResolver
{
    FeatureAccessSnapshot Resolve(AccessState accessState, UserRole role);

    bool IsEnabled(string featureName, AccessState accessState, UserRole role);
}
