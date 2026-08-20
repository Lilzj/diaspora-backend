namespace Diaspora.SharedKernel.Extensions;

public static class GuidExtensions
{
    public static bool IsEmpty(this Guid value) => value == Guid.Empty;

    public static Guid NewSequentialGuid() => Guid.CreateVersion7();
}
