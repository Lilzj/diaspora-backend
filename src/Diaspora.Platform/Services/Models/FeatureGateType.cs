namespace Diaspora.Platform.Services.Models;

public enum FeatureGateType
{
    Boolean = 0,
    Conditional = 1,
    RoleBased = 2,
    TrustBased = 3,
    CountryBased = 4,
    Percentage = 5
}
