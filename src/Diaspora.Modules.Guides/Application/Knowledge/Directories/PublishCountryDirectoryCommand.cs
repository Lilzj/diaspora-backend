using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Directories;

/// <summary>
/// Makes a country's knowledge directory visible to readers.
/// <para>
/// Separate from arranging it, because a directory is arranged over weeks and must not be
/// half-visible while it is. Publishing is the moment somebody decided the page is coherent.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.ManageDirectory)]
[Auditable]
public sealed record PublishCountryDirectoryCommand : ICommand
{
    public required string CountryCode { get; init; }
}
