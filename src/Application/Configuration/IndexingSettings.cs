using Microsoft.Extensions.Configuration;

namespace Storage.Application.Configuration;

public class IndexingSettings
{
    /// Enable or disable document indexing globally.
    /// Env var: STORAGE_INDEXING_ENABLED
    public bool Enabled { get; set; } = false;

    /// Elasticsearch node URL for the document index.
    /// Env var: INDEXING_ELASTIC_URL
    public string ElasticUrl { get; set; } = string.Empty;

    /// Elasticsearch index name for document metadata.
    /// Env var: STORAGE_ELASTIC_INDEX (defaults to "document-indexes")
    public string IndexName { get; set; } = "document-indexes";

    /// Basic-auth username for Elasticsearch. Empty disables authentication.
    /// Env var: INDEXING_ELASTIC_USERNAME
    public string Username { get; set; } = string.Empty;

    /// Basic-auth password for Elasticsearch.
    /// Env var: INDEXING_ELASTIC_PASSWORD
    public string Password { get; set; } = string.Empty;

    /// Skip TLS certificate validation (for self-signed in-cluster certs, e.g. ECK).
    /// Env var: INDEXING_ELASTIC_SKIP_CERT_VALIDATION (defaults to false)
    public bool SkipCertificateValidation { get; set; } = false;

    public static IndexingSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new IndexingSettings();

        var enabledStr = configuration["STORAGE_INDEXING_ENABLED"];
        if (!string.IsNullOrWhiteSpace(enabledStr) && bool.TryParse(enabledStr, out var enabled))
        {
            settings.Enabled = enabled;
        }

        if (settings.Enabled)
        {
            settings.ElasticUrl = configuration["INDEXING_ELASTIC_URL"]
                ?? throw new ArgumentNullException(nameof(configuration),
                    "STORAGE_INDEXING_ENABLED is true but INDEXING_ELASTIC_URL is not set.");

            var indexName = configuration["STORAGE_ELASTIC_INDEX"];
            if (!string.IsNullOrWhiteSpace(indexName))
            {
                settings.IndexName = indexName;
            }

            settings.Username = configuration["INDEXING_ELASTIC_USERNAME"] ?? string.Empty;
            settings.Password = configuration["INDEXING_ELASTIC_PASSWORD"] ?? string.Empty;

            var skipCertStr = configuration["INDEXING_ELASTIC_SKIP_CERT_VALIDATION"];
            if (!string.IsNullOrWhiteSpace(skipCertStr) && bool.TryParse(skipCertStr, out var skipCert))
            {
                settings.SkipCertificateValidation = skipCert;
            }
        }

        return settings;
    }
}
