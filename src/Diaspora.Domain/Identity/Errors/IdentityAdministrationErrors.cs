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

    // ─── The owner's rule for staff power (7 Nov 2026) ──────────────────────────────────

    /// <summary>
    /// Nobody acts on their own account: suspending, closing, reopening, reinstating, signing out
    /// or changing the role of the account they are signed in with.
    /// </summary>
    public static readonly Error CannotActOnOwnAccount = Error.Forbidden(
        "Identity.Administration.CannotActOnOwnAccount",
        "You cannot take this action on your own account. Ask another member of staff.");

    /// <summary>
    /// Staff act only on accounts strictly below their own rank: member &lt; Moderator &lt; Admin
    /// &lt; SuperAdmin. Until then a Moderator could suspend an Admin.
    /// </summary>
    public static Error TargetNotBelowYourRank(string targetRole) => Error.Forbidden(
        "Identity.Administration.TargetNotBelowYourRank",
        $"You can act only on accounts below your own rank. This account's role is {targetRole}.");

    /// <summary>The caller could not be found as an account, so their rank cannot be known.</summary>
    public static readonly Error ActorUnknown = Error.Forbidden(
        "Identity.Administration.ActorUnknown",
        "Your account could not be found, so this action cannot be checked against your rank.");

    /// <summary>A reason code that is not one of the platform's moderation reasons.</summary>
    public static Error UnknownReasonCode(string? code) => Error.Validation(
        "Identity.Administration.UnknownReasonCode",
        $"'{code}' is not one of the platform's moderation reasons.");

    /// <summary>A suspension's end date must be after the moment it is proposed or confirmed.</summary>
    public static readonly Error SuspensionEndHasPassed = Error.Validation(
        "Identity.Administration.SuspensionEndHasPassed",
        "The suspension's end date has passed. Decline or withdraw this and propose it again.");

    /// <summary>
    /// Granting Admin or SuperAdmin is a proposal a second member of staff confirms; this route
    /// changes a role at once and refuses to.
    /// </summary>
    public static readonly Error RoleGrantNeedsSecondStaff = Error.Validation(
        "Identity.Administration.RoleGrantNeedsSecondStaff",
        "Granting Admin or SuperAdmin needs a second member of staff. Propose it, and another member of staff confirms it.");

    /// <summary>Only a grant of Admin or SuperAdmin is proposed; every other role change is made at once.</summary>
    public static readonly Error RoleChangeIsImmediate = Error.Validation(
        "Identity.Administration.RoleChangeIsImmediate",
        "Only granting Admin or SuperAdmin is proposed. Change any other role directly.");

    /// <summary>The member already holds the role a proposal would give them.</summary>
    public static readonly Error AlreadyHoldsRole = Error.Conflict(
        "Identity.Administration.AlreadyHoldsRole",
        "The member already holds that role.");

    // ─── Proposals a second member of staff decides ─────────────────────────────────────

    public static readonly Error ProposalNotFound = Error.NotFound(
        "Identity.Administration.ProposalNotFound",
        "There is no such proposed account action.");

    /// <summary>One open proposal of a kind per account, so two colleagues do not race.</summary>
    public static readonly Error ProposalAlreadyPending = Error.Conflict(
        "Identity.Administration.ProposalAlreadyPending",
        "This action is already proposed for this account and waiting for a second member of staff.");

    public static readonly Error ProposalNotPending = Error.Conflict(
        "Identity.Administration.ProposalNotPending",
        "This proposal has already been confirmed, declined or withdrawn.");

    /// <summary>The point of the rule: the person who proposed an action is not its second pair of eyes.</summary>
    public static readonly Error ProposerCannotDecide = Error.Forbidden(
        "Identity.Administration.ProposerCannotDecide",
        "A different member of staff must confirm or decline what you proposed. You can withdraw it.");

    public static readonly Error OnlyProposerWithdraws = Error.Forbidden(
        "Identity.Administration.OnlyProposerWithdraws",
        "Only the member of staff who proposed this can withdraw it.");

    /// <summary>The decider does not hold the permission the proposed action itself needs.</summary>
    public static Error MissingPermission(string permission) => Error.Forbidden(
        "Identity.Administration.MissingPermission",
        $"Deciding this needs the permission to take the action itself ({permission}).");
}
