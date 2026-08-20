using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Xunit;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.UnitTests.Guides;

/// <summary>
/// Finding a free deep-link handle, and the difference between one somebody chose and one we
/// derived.
/// <para>
/// Two members genuinely writing "Open a bank account" in the same week is not an error, and a
/// derived handle is suffixed without comment because nobody chose it. A handle somebody typed
/// is different: quietly giving them <c>open-a-bank-account-2</c> is worse than telling them the
/// one they wanted is gone.
/// </para>
/// <para>
/// The allocator is an optimisation over the database's unique index, not a replacement for it —
/// check-then-insert is a race by construction. What it buys is that the ordinary collision
/// stops costing a constraint violation and a rolled-back transaction.
/// </para>
/// </summary>
public class GuideSlugAllocatorTests
{
    [Fact]
    public async Task A_title_becomes_a_handle_a_person_could_read_in_a_message()
    {
        var repository = new StubGuideRepository();

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            null, GuideLifecycleTests.Title("How to open a UK bank account"));

        allocated.IsSuccess.Should().BeTrue();
        allocated.Value.Value.Should().Be("how-to-open-a-uk-bank-account");
    }

    [Theory]
    [InlineData("Open a bank account (2026 edition)!", "open-a-bank-account-2026-edition")]
    [InlineData("NHS   registration --- step by step", "nhs-registration-step-by-step")]
    [InlineData("Council tax: what you owe & when", "council-tax-what-you-owe-when")]
    public async Task Punctuation_and_runs_collapse_to_single_hyphens(string title, string expected)
    {
        var allocated = await new GuideSlugAllocator(new StubGuideRepository())
            .AllocateAsync(null, GuideLifecycleTests.Title(title));

        allocated.Value.Value.Should().Be(expected);
    }

    [Fact]
    public async Task A_title_that_slugifies_to_nothing_still_gets_a_usable_handle()
    {
        // A title in a script with no ASCII letters is a real case on this platform, and the
        // guide still has to have a link.
        var allocated = await new GuideSlugAllocator(new StubGuideRepository())
            .AllocateAsync(null, GuideLifecycleTests.Title("한국어"));

        allocated.IsSuccess.Should().BeTrue();
        allocated.Value.Value.Should().Be("guide");
    }

    [Fact]
    public async Task A_taken_derived_handle_is_suffixed_without_comment()
    {
        var repository = new StubGuideRepository("how-to-open-a-uk-bank-account");

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            null, GuideLifecycleTests.Title("How to open a UK bank account"));

        allocated.IsSuccess.Should().BeTrue();
        allocated.Value.Value.Should().Be("how-to-open-a-uk-bank-account-2",
            "nobody chose this handle, so nobody is surprised by the number");
    }

    [Fact]
    public async Task Suffixes_climb_until_one_is_free()
    {
        var repository = new StubGuideRepository(
            "how-to-open-a-uk-bank-account",
            "how-to-open-a-uk-bank-account-2");

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            null, GuideLifecycleTests.Title("How to open a UK bank account"));

        allocated.Value.Value.Should().Be("how-to-open-a-uk-bank-account-3");
    }

    [Fact]
    public async Task A_handle_somebody_typed_is_taken_or_refused_and_never_renamed()
    {
        var repository = new StubGuideRepository("open-a-bank-account");

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            "open-a-bank-account", GuideLifecycleTests.Title());

        allocated.IsFailure.Should().BeTrue();
        allocated.Error.Should().Be(GuideErrors.SlugTaken,
            "somebody who typed a handle wants that handle, not a numbered variant of it");
    }

    [Fact]
    public async Task A_free_handle_somebody_typed_is_used_verbatim()
    {
        var allocated = await new GuideSlugAllocator(new StubGuideRepository()).AllocateAsync(
            "  Open-A-Bank-Account ", GuideLifecycleTests.Title());

        allocated.IsSuccess.Should().BeTrue();
        allocated.Value.Value.Should().Be("open-a-bank-account");
    }

    [Fact]
    public async Task A_handle_somebody_typed_that_is_not_a_handle_is_refused_before_any_lookup()
    {
        var repository = new StubGuideRepository();

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            "not a slug!", GuideLifecycleTests.Title());

        allocated.IsFailure.Should().BeTrue();
        allocated.Error.Should().Be(GuideErrors.SlugInvalid);
        repository.Lookups.Should().Be(0, "an invalid handle is not worth a round trip");
    }

    [Fact]
    public async Task Giving_up_is_a_conflict_rather_than_an_endless_search()
    {
        // Eleven guides with the same title is a different problem from a slug collision, and
        // spinning forever to find a twelfth handle would only hide it.
        var taken = new[] { "open-a-bank-account" }
            .Concat(Enumerable.Range(2, 10).Select(suffix => $"open-a-bank-account-{suffix}"))
            .ToArray();

        var repository = new StubGuideRepository(taken);

        var allocated = await new GuideSlugAllocator(repository).AllocateAsync(
            null, GuideLifecycleTests.Title("Open a bank account"));

        allocated.IsFailure.Should().BeTrue();
        allocated.Error.Should().Be(GuideErrors.SlugTaken);
    }

    [Fact]
    public async Task A_very_long_title_produces_a_handle_the_column_can_hold()
    {
        var title = string.Join(' ', Enumerable.Repeat("procedure", 60));

        var allocated = await new GuideSlugAllocator(new StubGuideRepository())
            .AllocateAsync(null, GuideLifecycleTests.Title(title[..GuideLimits.MaxTitle]));

        allocated.IsSuccess.Should().BeTrue();
        allocated.Value.Value.Length.Should().BeLessThanOrEqualTo(GuideLimits.MaxSlugLength);
        allocated.Value.Value.Should().NotEndWith("-");
    }

    /// <summary>
    /// A hand-written stub rather than a mock: the allocator's whole behaviour is a sequence of
    /// lookups, and a test that asserts on the sequence is clearer than one that configures it.
    /// </summary>
    private sealed class StubGuideRepository : IGuideRepository
    {
        private readonly HashSet<string> _taken;

        public StubGuideRepository(params string[] taken) =>
            _taken = new HashSet<string>(taken, StringComparer.Ordinal);

        /// <summary>How many times the allocator asked the database anything.</summary>
        public int Lookups { get; private set; }

        public Task<bool> SlugExistsAsync(GuideSlug slug, CancellationToken cancellationToken = default)
        {
            Lookups++;

            return Task.FromResult(_taken.Contains(slug.Value));
        }

        // Nothing else on the port is part of allocation. Throwing rather than returning null
        // keeps an accidental dependency on one of these visible.
        public Task<Guide?> GetAsync(GuideId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guide?> GetForReadAsync(GuideId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guide?> GetBySlugAsync(GuideSlug slug, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guide?> GetIncludingDeletedAsync(
            GuideId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CursorPagedResult<Guide>> QueryAsync(
            GuideQueryFilter filter,
            string? cursor,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountAsync(
            GuideQueryFilter filter, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Guide?> GetByVerificationIdAsync(
            Guid verificationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountByAuthorAsync(
            UserId authorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountInCountryAsync(
            string countryCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<int> CountInAreaAsync(
            string areaPathPrefix, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Guide>> GetScheduledDueAsync(
            DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Guide guide, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Remove(Guide guide) => throw new NotSupportedException();
    }
}
