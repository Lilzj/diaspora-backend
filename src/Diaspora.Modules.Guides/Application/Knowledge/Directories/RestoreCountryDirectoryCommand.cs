using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Brings an archived country directory back to the curator's desk.
/// <para>
/// <b>Restored unpublished, always.</b> It was archived because somebody decided the platform should
/// stop serving that country, and putting it back in front of readers is a second decision that
/// deserves a second look.
/// </para>
/// <para>
/// Read through the one repository member that sees past the global query filter — the row a restore
/// needs is exactly the row archiving hid.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record RestoreCountryDirectoryCommand : ICommand
{
    public required string CountryCode { get; init; }
}
