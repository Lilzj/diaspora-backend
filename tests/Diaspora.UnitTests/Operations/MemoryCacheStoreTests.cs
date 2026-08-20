using System.Text;
using Diaspora.Operations.Caching;
using Diaspora.Platform.Operations.Caching;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The local cache tier, on its own.
/// <para>
/// Worth testing separately from the cache that sits above it, because every property the cache
/// depends on — that a write is readable, that the age is measured from the write, that expiry is
/// honoured — is invisible from above: the cache swallows a store fault and reports a miss, so a store
/// that silently failed every write and a store that was merely cold would look identical.
/// </para>
/// </summary>
public sealed class MemoryCacheStoreTests
{
    [Fact]
    public async Task A_written_entry_is_readable()
    {
        var store = Build(out _);
        var payload = Encoding.UTF8.GetBytes("value");

        await store.SetAsync("k", payload, TimeSpan.FromMinutes(5));

        var read = await store.GetAsync("k");

        read.Found.Should().BeTrue();
        read.Source.Should().Be(CacheSource.Local);
        Encoding.UTF8.GetString(read.Value.Span).Should().Be("value");
    }

    [Fact]
    public async Task An_unknown_key_is_a_miss()
    {
        var store = Build(out _);

        (await store.GetAsync("absent")).Found.Should().BeFalse();
    }

    [Fact]
    public async Task A_removed_entry_is_a_miss()
    {
        var store = Build(out _);

        await store.SetAsync("k", Encoding.UTF8.GetBytes("v"), TimeSpan.FromMinutes(5));
        await store.RemoveAsync("k");

        (await store.GetAsync("k")).Found.Should().BeFalse();
    }

    [Fact]
    public async Task The_reported_age_is_measured_from_the_write()
    {
        var store = Build(out var clock);

        await store.SetAsync("k", Encoding.UTF8.GetBytes("v"), TimeSpan.FromHours(1));

        clock.Advance(TimeSpan.FromMinutes(7));

        (await store.GetAsync("k")).Age.Should().Be(TimeSpan.FromMinutes(7),
            "the tier above decides staleness from this number, and an IMemoryCache entry cannot " +
            "report its own write time — it either returns the value or has already dropped it");
    }

    [Fact]
    public async Task The_entry_limit_is_enforced_rather_than_ignored()
    {
        var store = Build(out _, limit: 64);

        // Well past the limit, so compaction has certainly run. Which entries survive is the
        // runtime's business; that the store stays bounded is the platform's.
        for (var index = 0; index < 5_000; index++)
        {
            await store.SetAsync(
                $"k{index}", Encoding.UTF8.GetBytes("v"), TimeSpan.FromMinutes(5));
        }

        var survivors = 0;

        for (var index = 0; index < 5_000; index++)
        {
            if ((await store.GetAsync($"k{index}")).Found)
            {
                survivors++;
            }
        }

        survivors.Should().BeLessThan(5_000,
            "an unbounded in-process cache is a memory leak with a friendly name");
    }

    private static MemoryCacheStore Build(out FakeClock clock, int limit = 20_000)
    {
        clock = new FakeClock();

        return new MemoryCacheStore(
            Options.Create(new CacheOptions { KeyPrefix = "test", LocalEntryLimit = limit }),
            clock);
    }
}
