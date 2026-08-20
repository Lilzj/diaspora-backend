namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>Where a community improvement suggestion is in its workflow.</summary>
public enum GuideSuggestionStatus
{
    Open = 0,
    Accepted = 1,
    Rejected = 2,
    Withdrawn = 3
}
