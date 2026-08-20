namespace Diaspora.Modules.Notifications;

/// <summary>
/// The ASP.NET authorization policies this module declares.
/// <para>
/// A policy is the endpoint-level gate; the permission attribute on the command is the handler-level
/// one. Both exist because they fail at different times: the policy refuses before a body is read,
/// the attribute refuses after the request is understood and can say which resource it was about.
/// </para>
/// <para>
/// <b>Lower case since B1.4.1, and the old names still work.</b> B13 shipped these as
/// <c>Notifications.Recipient</c> and siblings while every other module wrote its policies in lower
/// case; B1.4's <c>DSP5006</c> recorded the divergence as F3. The names below are the canonical ones
/// and <see cref="Legacy"/> holds what they used to be. Nothing about who may do what changed —
/// the permission each policy names is untouched, and a permission key is the contract, not this.
/// </para>
/// </summary>
public static class NotificationPolicies
{
    public const string Recipient = "notifications.recipient";
    public const string TemplateAuthor = "notifications.template-author";
    public const string Operator = "notifications.operator";

    /// <summary>
    /// The pre-B1.4.1 names, registered as aliases and authorising identically.
    /// <para>
    /// <b>They exist so the rename could not be the thing that breaks.</b> Renaming a constant
    /// fixes every reference the compiler can see, and an endpoint that reached the old name any
    /// other way would not fail loudly — an unregistered policy denies the request and logs
    /// nothing. These are deprecated from the moment they were written: no new code references
    /// them, and deleting them is a decision on its own, taken when the platform is satisfied
    /// nothing outside this repository names them.
    /// </para>
    /// </summary>
    public static class Legacy
    {
        public const string Recipient = "Notifications.Recipient";
        public const string TemplateAuthor = "Notifications.TemplateAuthor";
        public const string Operator = "Notifications.Operator";
    }
}
