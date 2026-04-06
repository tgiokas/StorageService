using Microsoft.AspNetCore.Mvc;

using Storage.Application.Dtos;
using Storage.Application.Interfaces;

namespace Storage.Api.Controllers;

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
    public async Task<IActionResult> Search(DocumentSearchDto request, CancellationToken ct = default)
    {
        var result = await _indexService.SearchAsync(request, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Get a document index entry by bucket and key.    
    [HttpGet("lookup")]
    public async Task<IActionResult> GetByBucketAndKey([FromQuery] string bucket, [FromQuery] string key, CancellationToken ct = default)
    {
        var result = await _indexService.GetByBucketAndKeyAsync(bucket, key, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }

    /// Update tags on a document index entry.    
    [HttpPost("tags")]
    public async Task<IActionResult> UpdateTags(TagsUpdateDto request, CancellationToken ct = default)
    {
        var result = await _indexService.UpdateTagsAsync(request.Id, request.Tags, ct);

        if (!result.Success)
        {
            return Accepted(result);
        }

        return Ok(result);
    }
}