using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

using Storage.Application.Dtos;
using Storage.Application.Interfaces;

namespace Storage.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentStorageService _storageService;

    public DocumentsController(IDocumentStorageService storageService)
    {
        _storageService = storageService;
    }

    /// Upload a document to a bucket.
    /// Tags are optional JSON key-value pairs for indexing (e.g. ?tags={"department":"hr","year":"2025"})    
    [HttpPost("{bucket}/upload")]
    [RequestSizeLimit(524_288_000)] // 500 MB
    public async Task<IActionResult> Upload(IFormFile file, string bucket, [FromQuery] string? key = null,
        [FromQuery] string? metadata = null,
        [FromQuery] string? uploadedBy = null,        
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result<string>.Fail("No file provided.", "STR-013"));
        }

        var objectKey = key ?? file.FileName;

        // Parse tags from JSON query string if provided
        Dictionary<string, string>? parsedTags = null;
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try
            {
                parsedTags = JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
            }
            catch
            {
                return BadRequest(Result<string>.Fail("Invalid tags format. Expected JSON object.", "STR-007"));
            }
        }

        using var stream = file.OpenReadStream();
        var request = new DocumentUploadDto
        {
            Bucket = bucket,
            Key = objectKey,
            Content = stream,
            ContentType = file.ContentType,
            UploadedBy = uploadedBy,
            Metadata = parsedTags
        };

        var result = await _storageService.UploadAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Download a document from a bucket.    
    [HttpGet("{bucket}/download")]
    public async Task<IActionResult> Download(string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _storageService.DownloadAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        var data = result.Data!;
        return File(data.Content, data.ContentType, data.FileName);
    }

    /// Delete a document from a bucket.    
    [HttpDelete("{bucket}")]
    public async Task<IActionResult> Delete(string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _storageService.DeleteAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Get metadata for a document.    
    [HttpGet("{bucket}/metadata")]
    public async Task<IActionResult> GetMetadata(string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _storageService.GetMetadataAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

}
