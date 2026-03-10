using Microsoft.Extensions.Configuration;

namespace Storage.Application.Configuration;

public class EncryptionSettings
{
    /// Enable or disable encryption globally.
    /// Env var: STORAGE_ENCRYPTION_ENABLED
    public bool Enabled { get; set; } = false;

    /// Base64-encoded 256-bit (32-byte) master key for AES-256-GCM.
    /// Env var: STORAGE_ENCRYPTION_MASTER_KEY
    public string MasterKeyBase64 { get; set; } = string.Empty;

    public static EncryptionSettings BindFromConfiguration(IConfiguration configuration)
    {
        var settings = new EncryptionSettings();

        var enabledStr = configuration["STORAGE_ENCRYPTION_ENABLED"];
        if (!string.IsNullOrWhiteSpace(enabledStr) && bool.TryParse(enabledStr, out var enabled))
        {
            settings.Enabled = enabled;
        }

        if (settings.Enabled)
        {
            settings.MasterKeyBase64 = configuration["STORAGE_ENCRYPTION_MASTER_KEY"]
                ?? throw new ArgumentNullException(nameof(configuration),
                    "ENCRYPTION_ENABLED is true but ENCRYPTION_MASTER_KEY is not set.");
        }

        return settings;
    }
}
