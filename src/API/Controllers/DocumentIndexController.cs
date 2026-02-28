using Microsoft.AspNetCore.Mvc;

using StorageService.Application.Dtos;
using StorageService.Application.Interfaces;

namespace StorageService.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class DocumentIndexController : ControllerBase
{
    private readonly IDocumentIndexService _indexService;

    public DocumentIndexController(IDocumentIndexService indexService)
    {
        _indexService = indexService;
    }

    /// Search the document index with filters, pagination, and sorting.
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] DocumentSearchDto request, CancellationToken ct = default)
    {
        var result = await _indexService.SearchAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Get a document index entry by its ID.
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _indexService.GetByIdAsync(id, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Get a document index entry by bucket and key.
    [HttpGet("{bucket}/lookup")]
    public async Task<IActionResult> GetByBucketAndKey(string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _indexService.GetByBucketAndKeyAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Update tags on a document index entry.
    [HttpPut("{id:guid}/tags")]
    public async Task<IActionResult> UpdateTags(Guid id, [FromBody] Dictionary<string, string> tags, CancellationToken ct = default)
    {
        var result = await _indexService.UpdateTagsAsync(id, tags, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Update custom metadata on a document index entry.
    [HttpPut("{id:guid}/metadata")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] Dictionary<string, string> metadata, CancellationToken ct = default)
    {
        var result = await _indexService.UpdateMetadataAsync(id, metadata, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }
}
