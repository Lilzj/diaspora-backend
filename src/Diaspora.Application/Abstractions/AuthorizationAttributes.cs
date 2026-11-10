namespace Diaspora.Application.Abstractions;

/// <summary>
/// The requirement a request places on its caller. One question per attribute, each with a
/// required argument (BSOLID-001).
/// <para>
/// <b>These replace a single <c>[Authorize]</c> carrying three independently nullable fields.</b>
/// That attribute had states nobody had ever evaluated, and two of them were wrong in opposite
/// directions: absent meant <em>admit everyone</em> (BSEC-003, and one route was reachable that
/// way), while <c>Policy</c> set meant <em>refuse everyone</em>, because the arm looked a dotted
/// policy name up as a colon-separated permission key. Neither was visible to a reader, a compiler
/// or an analyzer, because "which of these three fields did the author mean to set" is not a
/// question any of them can ask.
/// </para>
/// <para>
/// <b>Required constructor arguments are the whole point.</b> <c>[RequirePermission()]</c> does not
/// compile. There is no empty state to misread, so the only way to annotate a request wrongly is to
/// name the wrong thing — which is a mistake a reviewer can see, unlike an omission.
/// </para>
/// <para>
/// <b>There is no <c>[RequireRole]</c>, and that is deliberate.</b> BSOLID-001's remediation names
/// one, and the old attribute's <c>Roles</c> field had <b>zero users</b> across the whole
/// repository — every <c>Roles =</c> in the codebase is <c>PermissionDefinition.Roles</c>, a typed
/// set on the permission catalogue, which is the mechanism that actually works. So the finding's
/// "type the role" item had nothing to type: the field was dead, its only reader was a runtime
/// <c>Split(',')</c> in the behaviour, and it is deleted rather than typed. Adding an unused
/// attribute in its place would reproduce the defect this audit keeps finding — a declared
/// capability with no consumer, which reads as a control during review. When a role gate is
/// genuinely needed it gets an attribute then, typed, with a caller.
/// </para>
/// </summary>
public static class AuthorizationRequirements
{
    // A documentation anchor. The attributes below are the vocabulary; this type holds the
    // reasoning so that each one can stay short.
}

/// <summary>
/// The caller must hold a permission.
/// </summary>
/// <remarks>
/// The direct question, and the common one: around seven hundred requests ask it. The argument is a
/// permission key — colon-separated, <c>module:resource:action</c> — and the platform's permission
/// catalogue is validated at startup, so a key nothing declares fails the boot rather than denying
/// a route forever.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequirePermissionAttribute : Attribute
{
    public RequirePermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        Permission = permission;
    }

    /// <summary>The permission key, as <c>XPermissions</c> spells it.</summary>
    public string Permission { get; }
}

/// <summary>
/// The caller must satisfy a named authorisation policy.
/// </summary>
/// <remarks>
/// <para>
/// A policy is the name an endpoint uses and a permission is the thing being checked, so a request
/// behind a route that declares <c>RequireAuthorization(XPolicies.Y)</c> states <em>the same
/// constant</em> here. One name, declared once, which is what stops the route and the request
/// drifting apart.
/// </para>
/// <para>
/// Resolved through the policy catalogue, which is also what the host registers with ASP.NET Core —
/// so it covers the tier-backed policies a permission lookup structurally cannot express, and an
/// undeclared name is refused rather than ignored.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequirePolicyAttribute : Attribute
{
    public RequirePolicyAttribute(string policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policy);

        Policy = policy;
    }

    /// <summary>The policy name, dotted, as <c>XPolicies</c> spells it.</summary>
    public string Policy { get; }
}

/// <summary>
/// The caller must be authenticated, and nothing more is asked of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists so that "any signed-in member" stops being indistinguishable from a half-finished
/// thought.</b> It was spelled <c>[Authorize]</c> with no field set — the same text an author would
/// leave behind having meant to name a permission and been interrupted. Fourteen requests meant it
/// genuinely: ending your own session, exporting your own data, deleting your own account. There is
/// no permission a member could lack that should keep them signed in, and inventing one to satisfy
/// a vocabulary would have made those routes stricter than the controller in front of them.
/// </para>
/// <para>
/// The required reason is the difference. A reader can now tell the decision from the omission,
/// which is the whole of BSOLID-001 in one sentence.
/// </para>
/// <para>
/// <b>It authorises; it does not scope.</b> Every one of these requests is about the caller's own
/// data, and that is enforced by the handler reading the identity from the token rather than from
/// the request. This attribute says only that there is an identity to read.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequireAuthenticationAttribute : Attribute
{
    public RequireAuthenticationAttribute(string because)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(because);

        Because = because;
    }

    /// <summary>Why authentication alone is the right requirement here.</summary>
    public string Because { get; }
}
