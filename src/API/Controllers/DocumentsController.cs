using Microsoft.AspNetCore.Mvc;

using StorageService.Application.Dtos;
using StorageService.Application.Interfaces;

namespace StorageService.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentStorageService _storageService;

    public DocumentsController(IDocumentStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>
    /// Upload a document to a bucket.
    /// </summary>
    [HttpPost("{bucket}/upload")]
    [RequestSizeLimit(524_288_000)] // 500 MB
    public async Task<IActionResult> Upload(string bucket, IFormFile file, [FromQuery] string? key = null, CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(Result<string>.Fail("No file provided.", "STR-013"));
        }

        var objectKey = key ?? file.FileName;

        using var stream = file.OpenReadStream();
        var request = new UploadDocumentRequest
        {
            Bucket = bucket,
            Key = objectKey,
            Content = stream,
            ContentType = file.ContentType
        };

        var result = await _storageService.UploadAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Download a document from a bucket.
    /// </summary>
    [HttpGet("{bucket}/download/{*key}")]
    public async Task<IActionResult> Download(string bucket, string key, CancellationToken ct = default)
    {
        var result = await _storageService.DownloadAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        var data = result.Data!;
        return File(data.Content, data.ContentType, data.FileName);
    }

    /// <summary>
    /// Delete a document from a bucket.
    /// </summary>
    [HttpDelete("{bucket}/{*key}")]
    public async Task<IActionResult> Delete(string bucket, string key, CancellationToken ct = default)
    {
        var result = await _storageService.DeleteAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get metadata for a document.
    /// </summary>
    [HttpGet("{bucket}/metadata/{*key}")]
    public async Task<IActionResult> GetMetadata(string bucket, string key, CancellationToken ct = default)
    {
        var result = await _storageService.GetMetadataAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Check if a document exists in a bucket.
    /// </summary>
    [HttpHead("{bucket}/{*key}")]
    public async Task<IActionResult> Exists(string bucket, string key, CancellationToken ct = default)
    {
        var result = await _storageService.ExistsAsync(bucket, key, ct);

        if (!result.Success || !result.Data)
        {
            return NotFound();
        }

        return Ok();
    }

    /// <summary>
    /// List documents in a bucket, optionally filtered by prefix.
    /// </summary>
    [HttpGet("{bucket}")]
    public async Task<IActionResult> List(string bucket, [FromQuery] string? prefix = null, CancellationToken ct = default)
    {
        var result = await _storageService.ListAsync(bucket, prefix, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Generate a presigned URL for downloading a document.
    /// </summary>
    [HttpPost("{bucket}/presigned-url/{*key}")]
    public async Task<IActionResult> GetPresignedUrl(string bucket, string key, [FromQuery] int expiryMinutes = 60, CancellationToken ct = default)
    {
        var request = new PresignedUrlRequest
        {
            Bucket = bucket,
            Key = key,
            ExpiryMinutes = expiryMinutes
        };

        var result = await _storageService.GetPresignedUrlAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Ensure a bucket exists (create if it doesn't).
    /// </summary>
    [HttpPut("buckets/{bucket}")]
    public async Task<IActionResult> EnsureBucketExists(string bucket, CancellationToken ct = default)
    {
        var result = await _storageService.EnsureBucketExistsAsync(bucket, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }
}
