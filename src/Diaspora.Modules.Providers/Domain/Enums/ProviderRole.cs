namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// What one person may do to one provider.
/// <para>
/// The first four members are the frontend's <c>OrganizationRole</c> union
/// (<c>owner | manager | moderator | contributor</c>) exactly, and they serialise lowercase so
/// the client's <c>userRole</c> comparisons and its <c>activeRoleTab</c> filter keep working
/// unchanged. <see cref="Editor"/> is the brief's "delegated editors", which the client has no
/// name for yet.
/// </para>
/// <para>
/// Ordered by authority, and the order is load-bearing: every permission check in the module is
/// "is the caller's role at least X", which is one comparison rather than a switch that some
/// future member will be forgotten from. A franchise model — the brief names it as a future
/// requirement — adds a member above <see cref="Manager"/> without any call site changing.
/// </para>
/// </summary>
public enum ProviderRole
{
    /// <summary>
    /// May read, and appears in the provider's people list. The frontend's
    /// <c>contributor</c>: someone attached to the organization who cannot change it.
    /// </summary>
    Contributor = 1,

    /// <summary>
    /// May edit descriptive content — profile, gallery, services, hours — but not people,
    /// status, or verification. The brief's delegated editor.
    /// </summary>
    Editor = 2,

    /// <summary>
    /// An editor who may also act on the provider's social surface: hide a review, respond on
    /// the provider's behalf, report abuse. The frontend's <c>moderator</c>.
    /// </summary>
    Moderator = 3,

    /// <summary>
    /// Everything except transferring or ending ownership. May add and remove people below
    /// themselves, change status, and submit verification. The frontend's <c>manager</c>.
    /// </summary>
    Manager = 4,

    /// <summary>
    /// Full authority, including transferring ownership and deleting the provider. A provider
    /// always has at least one; removing the last one is refused.
    /// </summary>
    Owner = 5
}
