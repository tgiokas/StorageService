using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

using Storage.Domain.Entities;
using Storage.Domain.Interfaces;
using Storage.Application.Configuration;

namespace Storage.Infrastructure.Indexing;

public class ElasticDocumentIndexRepository : IDocumentIndexRepository
{
    private const string IndexNotFoundExceptionType = "index_not_found_exception";
    private const string ResourceAlreadyExistsExceptionType = "resource_already_exists_exception";

    private readonly ElasticsearchClient _elasticSearchClient;
    private readonly string _indexName;
    private readonly ILogger<ElasticDocumentIndexRepository> _logger;

    public ElasticDocumentIndexRepository(
        IOptions<IndexingSettings> options,
        ILogger<ElasticDocumentIndexRepository> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _indexName = settings.IndexName;

        var clientSettings = new ElasticsearchClientSettings(new Uri(settings.ElasticUrl))
            .DefaultIndex(_indexName);

        _elasticSearchClient = new ElasticsearchClient(clientSettings);

        _logger.LogInformation("Elasticsearch document index repository initialized: {Url}, index: {Index}",
            settings.ElasticUrl, _indexName);
    }

    public async Task<DocumentIndex?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.GetAsync<DocumentIndex>(id.ToString(), idx => idx.Index(_indexName), ct);

        if (!response.IsValidResponse || !response.Found)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
                _logger.LogWarning("Index {Index} does not exist yet. No documents to retrieve.", _indexName);

            return null;
        }

        var doc = response.Source!;
        doc.Id = id;
        return doc;
    }

    public async Task<DocumentIndex?> GetByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.SearchAsync<DocumentIndex>(s => s
            .Indices(_indexName)
            .Size(1)
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.Term(t => t.Field(f => f.Bucket).Value(bucket)),
                        m => m.Term(t => t.Field(f => f.Key).Value(key))
                    )
                )
            ),
            ct);

        if (!response.IsValidResponse || response.Documents.Count == 0)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
                _logger.LogWarning("Index {Index} does not exist yet. No documents to retrieve.", _indexName);

            return null;
        }

        var doc = response.Documents.First();
        if (response.Hits.Count > 0)
        {
            doc.Id = Guid.Parse(response.Hits.First().Id!);
        }
        return doc;
    }

    public async Task<List<DocumentIndex>> SearchAsync(DocumentIndexQuery query, CancellationToken ct = default)
    {
        var from = (query.Page - 1) * query.PageSize;

        var response = await _elasticSearchClient.SearchAsync<DocumentIndex>(s =>
        {
            s.Indices(_indexName)
             .From(from)
             .Size(query.PageSize)
             .Query(q => BuildQuery(q, query));

            ApplySorting(s, query.SortBy, query.SortDescending);
        }, ct);

        if (!response.IsValidResponse)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
            {
                _logger.LogWarning("Index {Index} does not exist yet. Returning empty results.", _indexName);
                return new List<DocumentIndex>();
            }

            _logger.LogError("Elasticsearch search failed: {Reason}", response.DebugInformation);
            throw new InvalidOperationException($"Elasticsearch search failed: {response.DebugInformation}");
        }

        var results = new List<DocumentIndex>();
        foreach (var hit in response.Hits)
        {
            var doc = hit.Source!;
            doc.Id = Guid.Parse(hit.Id!);
            results.Add(doc);
        }

        return results;
    }

    public async Task<int> CountAsync(DocumentIndexQuery query, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.CountAsync<DocumentIndex>(c => c
            .Indices(_indexName)
            .Query(q => BuildQuery(q, query)),
            ct);

        if (!response.IsValidResponse)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
            {
                _logger.LogWarning("Index {Index} does not exist yet. Returning zero count.", _indexName);
                return 0;
            }

            _logger.LogError("Elasticsearch count failed: {Reason}", response.DebugInformation);
            throw new InvalidOperationException($"Elasticsearch count failed: {response.DebugInformation}");
        }

        return (int)response.Count;
    }

    public async Task AddAsync(DocumentIndex document, CancellationToken ct = default)
    {
        await EnsureIndexExistsAsync(ct);

        var response = await _elasticSearchClient.IndexAsync(document, idx => idx
            .Index(_indexName)
            .Id(document.Id.ToString())
            .Refresh(Refresh.WaitFor),
            ct);

        if (!response.IsValidResponse)
        {
            _logger.LogError("Failed to index document {Id}: {Reason}", document.Id, response.DebugInformation);
            throw new InvalidOperationException($"Failed to index document: {response.DebugInformation}");
        }

        _logger.LogDebug("Indexed document {Id} to {Index}", document.Id, _indexName);
    }

    public async Task UpdateAsync(DocumentIndex document, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.UpdateAsync<DocumentIndex, DocumentIndex>(
            _indexName,
            document.Id.ToString(),
            u => u.Doc(document).Refresh(Refresh.WaitFor),
            ct);

        if (!response.IsValidResponse)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
            {
                _logger.LogWarning("Index {Index} does not exist yet. Cannot update document {Id}.", _indexName, document.Id);
                throw new InvalidOperationException($"Cannot update document — index '{_indexName}' does not exist yet.");
            }

            _logger.LogError("Failed to update document {Id}: {Reason}", document.Id, response.DebugInformation);
            throw new InvalidOperationException($"Failed to update document: {response.DebugInformation}");
        }

        _logger.LogDebug("Updated document {Id} in {Index}", document.Id, _indexName);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.DeleteAsync(_indexName, id.ToString(), ct);

        if (!response.IsValidResponse)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
            {
                _logger.LogDebug("Index {Index} does not exist yet. Nothing to delete for {Id}.", _indexName, id);
                return;
            }

            _logger.LogWarning("Failed to delete document {Id}: {Reason}", id, response.DebugInformation);
        }
    }

    public async Task DeleteByBucketAndKeyAsync(string bucket, string key, CancellationToken ct = default)
    {
        var response = await _elasticSearchClient.DeleteByQueryAsync<DocumentIndex>(d => d
            .Indices(_indexName)
            .Query(q => q
                .Bool(b => b
                    .Must(
                        m => m.Term(t => t.Field(f => f.Bucket).Value(bucket)),
                        m => m.Term(t => t.Field(f => f.Key).Value(key))
                    )
                )
            )
            .Refresh(true),
            ct);

        if (!response.IsValidResponse)
        {
            if (response.DebugInformation?.Contains(IndexNotFoundExceptionType) == true)
            {
                _logger.LogDebug("Index {Index} does not exist yet. Nothing to delete for {Bucket}/{Key}.", _indexName, bucket, key);
                return;
            }

            _logger.LogWarning("Failed to delete document by bucket/key {Bucket}/{Key}: {Reason}", bucket, key, response.DebugInformation);
        }
        else
        {
            _logger.LogDebug("Deleted {Count} document(s) from index for {Bucket}/{Key}", response.Deleted, bucket, key);
        }
    }

    // --- Private helpers ---

    private void BuildQuery(QueryDescriptor<DocumentIndex> q, DocumentIndexQuery query)
    {
        q.Bool(b =>
        {
            var musts = new List<Action<QueryDescriptor<DocumentIndex>>>();

            if (!string.IsNullOrWhiteSpace(query.Bucket))
                musts.Add(m => m.Term(t => t.Field(f => f.Bucket).Value(query.Bucket)));

            if (!string.IsNullOrWhiteSpace(query.KeyPrefix))
                musts.Add(m => m.Prefix(p => p.Field(f => f.Key).Value(query.KeyPrefix)));

            if (!string.IsNullOrWhiteSpace(query.FileName))
                musts.Add(m => m.Wildcard(w => w.Field(f => f.FileName).Value($"*{query.FileName}*").CaseInsensitive(true)));

            if (!string.IsNullOrWhiteSpace(query.ContentType))
                musts.Add(m => m.Term(t => t.Field(f => f.ContentType).Value(query.ContentType)));

            if (!string.IsNullOrWhiteSpace(query.UploadedBy))
                musts.Add(m => m.Term(t => t.Field(f => f.UploadedBy).Value(query.UploadedBy)));

            if (query.UploadedFrom.HasValue)
                musts.Add(m => m.Range(r => r.Date(dr => dr.Field(f => f.UploadedAt).Gte(query.UploadedFrom.Value))));

            if (query.UploadedTo.HasValue)
                musts.Add(m => m.Range(r => r.Date(dr => dr.Field(f => f.UploadedAt).Lte(query.UploadedTo.Value))));

            if (query.Tags != null && query.Tags.Count > 0)
            {
                foreach (var tag in query.Tags)
                {
                    var tagKey = tag.Key;
                    var tagValue = tag.Value;
                    musts.Add(m => m.Term(t => t.Field($"tags.{tagKey}").Value(tagValue)));
                }
            }

            if (musts.Count > 0)
                b.Must(musts.ToArray());
            else
                b.Must(m => m.MatchAll());
        });
    }

    private static void ApplySorting(SearchRequestDescriptor<DocumentIndex> s, string sortBy, bool descending)
    {
        var order = descending ? SortOrder.Desc : SortOrder.Asc;

        s.Sort(so =>
        {
            switch (sortBy.ToLowerInvariant())
            {
                case "filename":
                    so.Field(Infer.Field<DocumentIndex>(f => f.FileName), d => d.Order(order));
                    break;
                case "size":
                    so.Field(Infer.Field<DocumentIndex>(f => f.Size), d => d.Order(order));
                    break;
                case "contenttype":
                    so.Field(Infer.Field<DocumentIndex>(f => f.ContentType), d => d.Order(order));
                    break;
                case "uploadedat":
                default:
                    so.Field(Infer.Field<DocumentIndex>(f => f.UploadedAt), d => d.Order(order));
                    break;
            }
        });
    }

    private async Task EnsureIndexExistsAsync(CancellationToken ct)
    {
        var existsResponse = await _elasticSearchClient.Indices.ExistsAsync(_indexName, ct);

        if (existsResponse.Exists)
            return;

        var createResponse = await _elasticSearchClient.Indices.CreateAsync(_indexName, c => c
            .Mappings(m => m
                .Properties<DocumentIndex>(p => p
                    .Keyword(k => k.Bucket)
                    .Keyword(k => k.Key)
                    .Keyword(k => k.FileName)
                    .Keyword(k => k.ContentType)
                    .LongNumber(l => l.Size)
                    .Boolean(b => b.IsEncrypted)
                    .Keyword(k => k.UploadedBy)
                    .Date(d => d.UploadedAt)
                    .Date(d => d.ModifiedAt)
                    .Object(o => o.Tags)
                )
            ),
            ct);

        if (createResponse.IsValidResponse)
        {
            _logger.LogInformation("Created Elasticsearch index: {Index}", _indexName);
        }
        else if (createResponse.DebugInformation?.Contains(ResourceAlreadyExistsExceptionType) == true)
        {
            _logger.LogDebug("Index {Index} was created concurrently by another caller.", _indexName);
        }
        else
        {
            _logger.LogError("Failed to create Elasticsearch index {Index}: {Reason}",
                _indexName, createResponse.DebugInformation);
            throw new InvalidOperationException(
                $"Failed to create Elasticsearch index '{_indexName}': {createResponse.DebugInformation}");
        }
    }
}