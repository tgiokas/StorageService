using Microsoft.Extensions.Configuration;

namespace StorageService.Infrastructure.Configuration;

public class IndexingSettings
{
    /// Enable or disable document indexing globally.
    /// Env var: INDEXING_ENABLED
    public bool Enabled { get; set; } = false;

    /// Postgres connection string for the index database.
    /// Env var: INDEXING_DB_CONNECTION
    public string ConnectionString { get; set; } = string.Empty;

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
            settings.ConnectionString = configuration["INDEXING_DB_CONNECTION"]
                ?? throw new ArgumentNullException(nameof(configuration),
                    "INDEXING_ENABLED is true but INDEXING_DB_CONNECTION is not set.");
        }

        return settings;
    }
}
