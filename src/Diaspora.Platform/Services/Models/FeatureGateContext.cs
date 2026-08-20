using Diaspora.SharedKernel.Identity;

namespace Diaspora.Platform.Services.Models;

public sealed record FeatureGateContext
{
    public UserId? UserId { get; init; }
    public string? Role { get; init; }
    public TrustLevel? TrustLevel { get; init; }
    public string? Country { get; init; }
    public Dictionary<string, string> Properties { get; init; } = [];
}
