using System.ComponentModel.DataAnnotations;

namespace MandiDost.Api.Common;

/// <summary>Standard paging query used by list endpoints (listings, feed, chats…).</summary>
public class PageQuery
{
    [Range(1, 10_000)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public bool HasMore => Page * PageSize < TotalCount;
}
