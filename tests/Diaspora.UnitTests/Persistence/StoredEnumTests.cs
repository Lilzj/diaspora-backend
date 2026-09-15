using Diaspora.Persistence.Querying;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Diaspora.UnitTests.Persistence;

/// <summary>
/// "At least", "at most" and "in order of" over an enum stored by name, in real SQL — 15 Sep 2026.
/// <para>
/// The first test is the reason the helper exists: the plain operator runs, returns rows, and
/// returns the wrong ones. Seven modules shipped that shape. The rest prove the helper's range and
/// rank translate on a relational provider, where the words are all the database has.
/// </para>
/// </summary>
public sealed class StoredEnumTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly StoredEnumTestContext _context;

    public StoredEnumTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new StoredEnumTestContext(
            new DbContextOptionsBuilder<StoredEnumTestContext>().UseSqlite(_connection).Options);

        _context.Database.EnsureCreated();

        // Declared Low, Normal, High, Urgent. Alphabetically: High, Low, Normal, Urgent.
        _context.Tickets.AddRange(
            new StoredEnumTestTicket { Id = 1, Severity = StoredEnumTestSeverity.Low },
            new StoredEnumTestTicket { Id = 2, Severity = StoredEnumTestSeverity.Normal },
            new StoredEnumTestTicket { Id = 3, Severity = StoredEnumTestSeverity.High },
            new StoredEnumTestTicket { Id = 4, Severity = StoredEnumTestSeverity.Urgent });

        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public void The_plain_operator_compares_the_stored_words()
    {
        var severities = _context.Tickets
            .Where(ticket => ticket.Severity >= StoredEnumTestSeverity.Normal)
            .Select(ticket => ticket.Severity)
            .ToList();

        // Normal and Urgent: "High" sorts before "Normal", so the high-severity ticket is lost.
        severities.Should().BeEquivalentTo(
            [StoredEnumTestSeverity.Normal, StoredEnumTestSeverity.Urgent],
            "this is the defect; if a provider ever compares ranks here the helper can go");
    }

    [Fact]
    public void At_least_admits_by_rank_in_sql()
    {
        var admitted = StoredEnum.AtLeast(StoredEnumTestSeverity.Normal);

        _context.Tickets
            .Where(ticket => admitted.Contains(ticket.Severity))
            .Select(ticket => ticket.Severity)
            .ToList()
            .Should().BeEquivalentTo(
                [StoredEnumTestSeverity.Normal, StoredEnumTestSeverity.High, StoredEnumTestSeverity.Urgent]);
    }

    [Fact]
    public void At_most_admits_by_rank_in_sql()
    {
        var admitted = StoredEnum.AtMost(StoredEnumTestSeverity.High);

        _context.Tickets
            .Where(ticket => admitted.Contains(ticket.Severity))
            .Select(ticket => ticket.Severity)
            .ToList()
            .Should().BeEquivalentTo(
                [StoredEnumTestSeverity.Low, StoredEnumTestSeverity.Normal, StoredEnumTestSeverity.High]);
    }

    [Fact]
    public void Rank_orders_by_the_declared_order_in_sql()
    {
        _context.Tickets
            .OrderByDescending(StoredEnum.Rank((StoredEnumTestTicket ticket) => ticket.Severity))
            .Select(ticket => ticket.Severity)
            .ToList()
            .Should().Equal(
                StoredEnumTestSeverity.Urgent,
                StoredEnumTestSeverity.High,
                StoredEnumTestSeverity.Normal,
                StoredEnumTestSeverity.Low);
    }

    [Fact]
    public void Ranges_follow_the_values_not_their_positions()
    {
        // Spaced values, as MembershipRole's are.
        StoredEnum.AtLeast(StoredEnumTestRole.Moderator).Should().Equal(
            StoredEnumTestRole.Moderator, StoredEnumTestRole.Administrator, StoredEnumTestRole.Owner);

        StoredEnum.AtMost(StoredEnumTestRole.Volunteer).Should().Equal(
            StoredEnumTestRole.Member, StoredEnumTestRole.Volunteer);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

public enum StoredEnumTestSeverity
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum StoredEnumTestRole
{
    Member = 0,
    Volunteer = 10,
    Moderator = 20,
    Administrator = 40,
    Owner = 50
}

public sealed class StoredEnumTestTicket
{
    public int Id { get; set; }

    public StoredEnumTestSeverity Severity { get; set; }
}

public sealed class StoredEnumTestContext(DbContextOptions<StoredEnumTestContext> options) : DbContext(options)
{
    public DbSet<StoredEnumTestTicket> Tickets => Set<StoredEnumTestTicket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<StoredEnumTestTicket>()
            .Property(ticket => ticket.Severity)
            .HasConversion<string>();
    }
}
