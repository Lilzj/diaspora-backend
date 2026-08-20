namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Where a guide's trust mark stands. A projection of Trust's decision — nothing in this
/// module can move a guide to Verified except a handler for a Trust integration event.
/// </summary>
public enum GuideVerificationState
{
    None = 0,
    Pending = 1,
    Verified = 2,
    Revoked = 3
}
