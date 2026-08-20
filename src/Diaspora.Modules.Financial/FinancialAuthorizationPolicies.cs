namespace Diaspora.Modules.Financial;

/// <summary>
/// The ASP.NET authorization policies this module declares.
/// <para>
/// A policy is the endpoint-level gate; the permission attribute on the command is the handler-level
/// one. Both exist because they fail at different times: the policy refuses before a body is read,
/// the attribute refuses after the request is understood and can say which resource it was about.
/// </para>
/// <para>
/// <b>Named <c>FinancialAuthorizationPolicies</c> rather than <c>FinancialPolicies</c>, and the extra
/// word is load-bearing.</b> This module already uses "policy" for the domain concept that decides a
/// price — a <see cref="Domain.Policies.FinancialPolicy"/>. Two unrelated things called
/// <c>FinancialPolicy</c> in one assembly is the kind of collision that survives review and then
/// costs an afternoon; B14 recorded exactly the same reasoning for the same reason.
/// </para>
/// <para>
/// Lower case, matching every module since B1.4.1. No legacy aliases, because nothing has ever been
/// published under another name.
/// </para>
/// </summary>
public static class FinancialAuthorizationPolicies
{
    /// <summary>A member acting on their own money.</summary>
    public const string Member = "financial.member";

    /// <summary>Staff operating the platform's finances.</summary>
    public const string Operator = "financial.operator";

    /// <summary>Staff reading financial reporting.</summary>
    public const string Analyst = "financial.analyst";
}
