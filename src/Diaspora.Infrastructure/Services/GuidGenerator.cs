using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Extensions;

namespace Diaspora.Infrastructure.Services;

public sealed class GuidGenerator : IGuidGenerator
{
    public Guid NewGuid() => Guid.NewGuid();
    public Guid NewSequentialGuid() => GuidExtensions.NewSequentialGuid();
}
