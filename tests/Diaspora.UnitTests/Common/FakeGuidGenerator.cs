using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.UnitTests.Common;

public sealed class FakeGuidGenerator : IGuidGenerator
{
    private readonly Queue<Guid> _guids = new();
    private int _counter;

    public void Enqueue(params Guid[] guids)
    {
        foreach (var guid in guids) _guids.Enqueue(guid);
    }

    public Guid NewGuid() =>
        _guids.Count > 0
            ? _guids.Dequeue()
            : new Guid($"00000000-0000-0000-0000-{(++_counter):D12}");

    public Guid NewSequentialGuid() => NewGuid();
}
