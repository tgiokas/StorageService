using Microsoft.EntityFrameworkCore;

using StorageService.Domain.Entities;
using StorageService.Domain.Interfaces;
using StorageService.Infrastructure.Database;

namespace StorageService.Infrastructure.Repositories;

public class DocumentIndexRepository : IDocumentIndexRepository
{
    private readonly StorageDbContext _dbContext;

    public DocumentIndexRepository(StorageDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DocumentIndex?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.DocumentIndexes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<DocumentIndex?> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default)
    {
        return await _dbContext.DocumentIndexes
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Bucket == bucket && d.Key == key, ct);
    }

    public async Task<List<DocumentIndex>> SearchAsync(DocumentIndexQuery query, CancellationToken ct = default)
    {
        var queryable = BuildQuery(query);

        // Sorting
        queryable = ApplySorting(queryable, query.SortBy, query.SortDescending);

        // Pagination
        var skip = (query.Page - 1) * query.PageSize;
        queryable = queryable.Skip(skip).Take(query.PageSize);

        return await queryable.ToListAsync(ct);
    }

    public async Task<int> CountAsync(DocumentIndexQuery query, CancellationToken ct = default)
    {
        var queryable = BuildQuery(query);
        return await queryable.CountAsync(ct);
    }

    public async Task AddAsync(DocumentIndex document, CancellationToken ct = default)
    {
        await _dbContext.DocumentIndexes.AddAsync(document, ct);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DocumentIndex document, CancellationToken ct = default)
    {
        _dbContext.DocumentIndexes.Update(document);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var document = await _dbContext.DocumentIndexes.FindAsync(new object[] { id }, ct);
        if (document != null)
        {
            _dbContext.DocumentIndexes.Remove(document);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default)
    {
        var document = await _dbContext.DocumentIndexes
            .FirstOrDefaultAsync(d => d.Bucket == bucket && d.Key == key, ct);

        if (document != null)
        {
            _dbContext.DocumentIndexes.Remove(document);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private IQueryable<DocumentIndex> BuildQuery(DocumentIndexQuery query)
    {
        var queryable = _dbContext.DocumentIndexes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Bucket))
            queryable = queryable.Where(d => d.Bucket == query.Bucket);

        if (!string.IsNullOrWhiteSpace(query.KeyPrefix))
            queryable = queryable.Where(d => d.Key.StartsWith(query.KeyPrefix));

        if (!string.IsNullOrWhiteSpace(query.FileName))
            queryable = queryable.Where(d => d.FileName.Contains(query.FileName));

        if (!string.IsNullOrWhiteSpace(query.ContentType))
            queryable = queryable.Where(d => d.ContentType == query.ContentType);

        if (!string.IsNullOrWhiteSpace(query.UploadedBy))
            queryable = queryable.Where(d => d.UploadedBy == query.UploadedBy);

        if (query.UploadedFrom.HasValue)
            queryable = queryable.Where(d => d.UploadedAt >= query.UploadedFrom.Value);

        if (query.UploadedTo.HasValue)
            queryable = queryable.Where(d => d.UploadedAt <= query.UploadedTo.Value);

        // JSONB tag filtering: all specified tags must match
        if (query.Tags != null && query.Tags.Count > 0)
        {
            foreach (var tag in query.Tags)
            {
                var tagKey = tag.Key;
                var tagValue = tag.Value;
                queryable = queryable.Where(d =>
                    d.Tags != null &&
                    d.Tags.ContainsKey(tagKey) &&
                    d.Tags[tagKey] == tagValue);
            }
        }

        return queryable;
    }

    private static IQueryable<DocumentIndex> ApplySorting(IQueryable<DocumentIndex> queryable, string sortBy, bool descending)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "filename" => descending
                ? queryable.OrderByDescending(d => d.FileName)
                : queryable.OrderBy(d => d.FileName),
            "size" => descending
                ? queryable.OrderByDescending(d => d.Size)
                : queryable.OrderBy(d => d.Size),
            "contenttype" => descending
                ? queryable.OrderByDescending(d => d.ContentType)
                : queryable.OrderBy(d => d.ContentType),
            "uploadedat" or _ => descending
                ? queryable.OrderByDescending(d => d.UploadedAt)
                : queryable.OrderBy(d => d.UploadedAt),
        };
    }
}
