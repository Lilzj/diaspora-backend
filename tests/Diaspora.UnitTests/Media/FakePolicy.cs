using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Extensibility.Access;

namespace Diaspora.UnitTests.Media;

/// <summary>A vertical's access policy, returning a fixed verdict.</summary>
internal sealed class FakePolicy : IMediaAccessPolicy
{
    private readonly MediaAccessDecision _decision;

    public FakePolicy(MediaOwnerType ownerType, MediaAccessDecision decision)
    {
        OwnerType = ownerType;
        _decision = decision;
    }

    public MediaOwnerType OwnerType { get; }

    public Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(_decision);
}
