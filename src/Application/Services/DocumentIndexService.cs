using Microsoft.Extensions.Logging;

using Storage.Application.Dtos;
using Storage.Application.Errors;
using Storage.Application.Interfaces;
using Storage.Domain.Entities;
using Storage.Domain.Interfaces;

namespace Storage.Application.Services;

public class DocumentIndexService : IDocumentIndexService
{
    private readonly IDocumentIndexRepository _repository;
    private readonly IErrorCatalog _errors;
    private readonly ILogger<DocumentIndexService> _logger;

    public DocumentIndexService(
        IDocumentIndexRepository repository,
        IErrorCatalog errors,
        ILogger<DocumentIndexService> logger)
    {
        _repository = repository;
        _errors = errors;
        _logger = logger;
    }

    public async Task<Result<DocumentIndexDto>> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default)
    {
        try
        {
            var doc = await _repository.GetByBucketAndKeyAsync(bucket, key, ct);
            if (doc == null)
                return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexEntryNotFound);

            return Result<DocumentIndexDto>.Ok(MapToDto(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get index entry for {Bucket}/{Key}", bucket, key);
            return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexQueryFailed);
        }
    }

    public async Task<Result<PagedResultDto<DocumentIndexDto>>> SearchAsync(DocumentSearchDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SearchText))
            return _errors.Fail<PagedResultDto<DocumentIndexDto>>(ErrorCodes.STORAGE.SearchTextRequired);

        try
        {
            var query = new DocumentIndexQuery
            {
                SearchText = request.SearchText,
                PageNumber = request.PageNumber ?? 1,
                PageSize = request.PageSize ?? 10,
                SortBy = request.SortBy ?? "UploadedAt",
                SortDescending = request.SortDescending ?? true
            };

            var (results, total) = await _repository.SearchAsync(query, ct);

            var pagedResult = new PagedResultDto<DocumentIndexDto>
            {
                Results = results.Select(MapToDto).ToList(),
                CurrentPage = query.PageNumber,
                PageSize = query.PageSize,
                TotalCount = total
            };

            return Result<PagedResultDto<DocumentIndexDto>>.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search document index");
            return _errors.Fail<PagedResultDto<DocumentIndexDto>>(ErrorCodes.STORAGE.IndexQueryFailed);
        }
    }

    public async Task<Result<DocumentIndexDto>> UpdateTagsAsync(
        string bucket, string key, Dictionary<string, string> tags, CancellationToken ct = default)
    {
        try
        {
            var doc = await _repository.GetByBucketAndKeyAsync(bucket, key, ct);
            if (doc == null)
                return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexEntryNotFound);

            doc.Tags = tags;            
            await _repository.UpdateAsync(doc, ct);

            _logger.LogInformation("Updated tags for index entry {Bucket}/{Key}", bucket, key);
            return Result<DocumentIndexDto>.Ok(MapToDto(doc), "Tags updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tags for index entry {Bucket}/{Key}", bucket, key);
            return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexUpdateFailed);
        }
    }

    private static DocumentIndexDto MapToDto(DocumentIndex doc) => new()
    {
        Id = doc.Id,
        Bucket = doc.Bucket,
        Key = doc.Key,
        FileName = doc.FileName,
        UploadedAt = doc.UploadedAt,
        Tags = doc.Tags
    };
}