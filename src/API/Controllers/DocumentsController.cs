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

    [HttpGet("list")]
    public async Task<IActionResult> ListObjects(
    [FromQuery] string bucket,
    [FromQuery] string? prefix = null,
    [FromQuery] bool recursive = false,
    CancellationToken ct = default)
    {
        var result = await _storageService.ListObjectsAsync(bucket, prefix, recursive, ct);
        if (!result.Success) return Accepted(result);
        return Ok(result);
    }

    /// Upload a document to a bucket.
    /// Tags are optional JSON key-value pairs for indexing (e.g. ?metadata={"department":"hr","year":"2025"})    
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file, 
        [FromForm] string bucket,
        [FromForm] string? key = null,
        [FromForm] string? metadata = null,
        [FromForm] string? uploadedBy = null, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result<string>.Fail("No file provided.", "STR-024"));
        }

        var objectKey = key ?? file.FileName;        

        // Parse tags from JSON query string if provided
        Dictionary<string, string>? parsedTags = null;
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try
            {
                metadata = metadata.Trim().Trim('"').Replace("\\\"", "\"");
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
    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _storageService.DownloadAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        var data = result.Data!;
        return File(data.Content, data.ContentType, data.FileName);
    }

    /// Delete a list of documents (i.e bucket + key) Supports batch operations up to 100 items.
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(List<DocumentLocatorDto> request, CancellationToken ct = default)
    {
        var result = await _storageService.DeleteAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Get metadata for a document.    
    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata([FromQuery] string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _storageService.GetMetadataAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Move documents from one bucket/key to another bucket/key.
    /// Supports batch operations up to 100 items.
    [HttpPost("move")]
    public async Task<IActionResult> Move(DocumentBatchMoveDto request, CancellationToken ct = default)
    {
        var result = await _storageService.MoveAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }
}