using System.Globalization;
using Diaspora.SharedKernel.Pagination;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.SharedKernel;

public class PaginationTests
{
    [Fact]
    public void PagedResult_should_calculate_metadata()
    {
        var items = Enumerable.Range(1, 20).ToList();
        var result = PagedResult<int>.Create(items, page: 2, pageSize: 20, totalCount: 100);

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(20);
        result.TotalCount.Should().Be(100);
        result.TotalPages.Should().Be(5);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void PagedResult_map_should_preserve_metadata()
    {
        var items = new List<int> { 1, 2, 3 };
        var result = PagedResult<int>.Create(items, 1, 10, 3);

        var mapped = result.Map(x => x.ToString(CultureInfo.InvariantCulture));

        mapped.Items.Should().BeEquivalentTo(["1", "2", "3"]);
        mapped.TotalCount.Should().Be(3);
    }

    [Fact]
    public void PaginationRequest_sanitize_should_clamp_values()
    {
        var request = new PaginationRequest { Page = -1, PageSize = 200 };

        var sanitized = request.Sanitize();

        sanitized.Page.Should().Be(1);
        sanitized.PageSize.Should().Be(PaginationRequest.MaxPageSize);
    }
}
