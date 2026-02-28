using Microsoft.Extensions.Logging;

using StorageService.Application.Dtos;
using StorageService.Application.Errors;
using StorageService.Application.Interfaces;
using StorageService.Domain.Entities;
using StorageService.Domain.Interfaces;

namespace StorageService.Application.Services;

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

    public async Task<Result<DocumentIndexDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var doc = await _repository.GetByIdAsync(id, ct);
            if (doc == null)
                return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexEntryNotFound);

            return Result<DocumentIndexDto>.Ok(MapToDto(doc));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get index entry by id {Id}", id);
            return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexQueryFailed);
        }
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
        try
        {
            var query = new DocumentIndexQuery
            {
                Bucket = request.Bucket,
                KeyPrefix = request.KeyPrefix,
                FileName = request.FileName,
                ContentType = request.ContentType,
                UploadedBy = request.UploadedBy,
                UploadedFrom = request.UploadedFrom,
                UploadedTo = request.UploadedTo,
                Tags = request.Tags,
                Page = request.Page,
                PageSize = request.PageSize,
                SortBy = request.SortBy,
                SortDescending = request.SortDescending
            };

            var results = await _repository.SearchAsync(query, ct);
            var total = await _repository.CountAsync(query, ct);

            var pagedResult = new PagedResultDto<DocumentIndexDto>
            {
                Results = results.Select(MapToDto).ToList(),
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                Total = total
            };

            return Result<PagedResultDto<DocumentIndexDto>>.Ok(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search document index");
            return _errors.Fail<PagedResultDto<DocumentIndexDto>>(ErrorCodes.STORAGE.IndexQueryFailed);
        }
    }

    public async Task<Result<DocumentIndexDto>> UpdateTagsAsync(Guid id, Dictionary<string, string> tags, CancellationToken ct = default)
    {
        try
        {
            var doc = await _repository.GetByIdAsync(id, ct);
            if (doc == null)
                return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexEntryNotFound);

            doc.Tags = tags;
            doc.LastModified = DateTime.UtcNow;
            await _repository.UpdateAsync(doc, ct);

            _logger.LogInformation("Updated tags for index entry {Id}", id);
            return Result<DocumentIndexDto>.Ok(MapToDto(doc), "Tags updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tags for index entry {Id}", id);
            return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexUpdateFailed);
        }
    }

    public async Task<Result<DocumentIndexDto>> UpdateMetadataAsync(Guid id, Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        try
        {
            var doc = await _repository.GetByIdAsync(id, ct);
            if (doc == null)
                return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexEntryNotFound);

            doc.CustomMetadata = metadata;
            doc.LastModified = DateTime.UtcNow;
            await _repository.UpdateAsync(doc, ct);

            _logger.LogInformation("Updated custom metadata for index entry {Id}", id);
            return Result<DocumentIndexDto>.Ok(MapToDto(doc), "Metadata updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update metadata for index entry {Id}", id);
            return _errors.Fail<DocumentIndexDto>(ErrorCodes.STORAGE.IndexUpdateFailed);
        }
    }

    private static DocumentIndexDto MapToDto(DocumentIndex doc) => new()
    {
        Id = doc.Id,
        Bucket = doc.Bucket,
        Key = doc.Key,
        FileName = doc.FileName,
        ContentType = doc.ContentType,
        Size = doc.Size,
        ETag = doc.ETag,
        IsEncrypted = doc.IsEncrypted,
        UploadedBy = doc.UploadedBy,
        UploadedAt = doc.UploadedAt,
        LastModified = doc.LastModified,
        Tags = doc.Tags,
        CustomMetadata = doc.CustomMetadata
    };
}
