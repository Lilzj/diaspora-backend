using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.UnitTests.Platform;

/// <summary>
/// Small hand-built contributors for the catalogue tests, so each test composes exactly the
/// declarations it is reasoning about rather than the whole platform's.
/// </summary>
internal sealed class TestPermissionContributor : IPermissionContributor
{
    private readonly PermissionDefinition[] _permissions;

    public TestPermissionContributor(string module, params PermissionDefinition[] permissions)
    {
        Module = module;
        _permissions = permissions;
    }

    public string Module { get; }

    public IEnumerable<PermissionDefinition> GetPermissions() => _permissions;
}

internal sealed class TestFeatureContributor : IFeatureContributor
{
    private readonly FeatureDefinition[] _features;

    public TestFeatureContributor(string module, params FeatureDefinition[] features)
    {
        Module = module;
        _features = features;
    }

    public string Module { get; }

    public IEnumerable<FeatureDefinition> GetFeatures() => _features;
}

internal sealed class TestPolicyContributor : IPolicyContributor
{
    private readonly PolicyDefinition[] _policies;

    public TestPolicyContributor(string module, params PolicyDefinition[] policies)
    {
        Module = module;
        _policies = policies;
    }

    public string Module { get; }

    public IEnumerable<PolicyDefinition> GetPolicies() => _policies;
}
