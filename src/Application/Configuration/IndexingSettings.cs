using Microsoft.Extensions.Configuration;

namespace Storage.Application.Configuration;

public class IndexingSettings
{
    /// Enable or disable document indexing globally.
    /// Env var: INDEXING_ENABLED
    public bool Enabled { get; set; } = false;

    /// Elasticsearch node URL for the document index.
    /// Env var: INDEXING_ELASTIC_URL
    /// Example: http://elasticsearch:9200
    public string ElasticUrl { get; set; } = string.Empty;

    /// Elasticsearch index name for document metadata.
    /// Env var: INDEXING_ELASTIC_INDEX (optional, defaults to "document-indexes")
    public string IndexName { get; set; } = "document-indexes";

    public static IndexingSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new IndexingSettings();

        var enabledStr = configuration["INDEXING_ENABLED"];
        if (!string.IsNullOrWhiteSpace(enabledStr) && bool.TryParse(enabledStr, out var enabled))
        {
            settings.Enabled = enabled;
        }

        if (settings.Enabled)
        {
            settings.ElasticUrl = configuration["INDEXING_ELASTIC_URL"]
                ?? throw new ArgumentNullException(nameof(configuration),
                    "INDEXING_ENABLED is true but INDEXING_ELASTIC_URL is not set.");

            var indexName = configuration["INDEXING_ELASTIC_INDEX"];
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                settings.IndexName = indexName;
            }
        }

        return settings;
    }
}
