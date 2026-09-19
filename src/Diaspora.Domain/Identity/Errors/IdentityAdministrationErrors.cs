using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Errors;

/// <summary>
/// Refusals that belong to staff acting on somebody else's account.
/// <para>
/// Separate from <see cref="UserErrors"/> because the audience is different: these are read
/// by a colleague in a console, not by a member, and they can say exactly what was refused
/// and why. A member is never shown any of them.
/// </para>
/// </summary>
public static class IdentityAdministrationErrors
{
    /// <summary>
    /// Nobody changes their own role.
    /// <para>
    /// It closes the obvious escalation — an Admin granting themselves SuperAdmin — and the
    /// less obvious accident, an administrator demoting themselves out of the console they
    /// are standing in.
    /// </para>
    /// </summary>
    public static readonly Error CannotChangeOwnRole = Error.Forbidden(
        "Identity.Administration.CannotChangeOwnRole",
        "You cannot change your own role. Ask another administrator.");

    /// <summary>
    /// Only a SuperAdmin grants or removes SuperAdmin.
    /// <para>
    /// A platform where the second rung can appoint the first has no first rung.
    /// </para>
    /// </summary>
    public static readonly Error SuperAdminOnly = Error.Forbidden(
        "Identity.Administration.SuperAdminOnly",
        "Only a SuperAdmin can grant or remove the SuperAdmin role.");

    /// <summary>
    /// The role named is not one this platform has.
    /// <para>
    /// Named rather than defaulted on purpose. A role string the platform cannot parse must
    /// never quietly become the lowest role — that would turn a typo into a demotion.
    /// </para>
    /// </summary>
    public static Error UnknownRole(string? role) => Error.Validation(
        "Identity.Administration.UnknownRole",
        $"'{role}' is not a role on this platform.");
}
