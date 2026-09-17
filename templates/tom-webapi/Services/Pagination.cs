using Microsoft.EntityFrameworkCore;

namespace Tom.WebApi.Services;

public sealed class PaginatedItems<TEntity>(int pageIndex, int pageSize, long count, IEnumerable<TEntity> data)
    where TEntity : class
{
    public int PageIndex { get; } = pageIndex;

    public int PageSize { get; } = pageSize;

    public long TotalItems { get; } = count;

    public int TotalPages { get; } = pageSize <= 0 ? 1 : (int)Math.Ceiling(count / (double)pageSize);

    public bool HasNextPage { get; } = pageSize > 0 && pageIndex < (int)Math.Ceiling(count / (double)pageSize);

    public IEnumerable<TEntity> Data { get; } = data;

    public static async Task<PaginatedItems<TEntity>> CreateAsync(
        int pageIndex,
        int pageSize,
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
    {
        var totalItems = await query.LongCountAsync(cancellationToken);
        var normalizedIndex = Math.Max(pageIndex, 1) - 1;

        query = query.AsNoTracking();

        if (pageSize >= 0)
        {
            query = query
                .Skip(pageSize * normalizedIndex)
                .Take(pageSize);
        }

        var dataInPage = await query.ToListAsync(cancellationToken);
        return new PaginatedItems<TEntity>(pageIndex, pageSize, totalItems, dataInPage);
    }
}

public static class Pagination
{
    public static Task<PaginatedItems<TEntity>> CreateAsync<TEntity>(
        int pageIndex,
        int pageSize,
        IQueryable<TEntity> query,
        CancellationToken cancellationToken = default)
        where TEntity : class
        => PaginatedItems<TEntity>.CreateAsync(pageIndex, pageSize, query, cancellationToken);
}

public sealed record PaginationRequest(int PageSize = 10, int PageIndex = 1);
