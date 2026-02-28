using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StorageService.Domain.Interfaces;
using StorageService.Infrastructure.Configuration;

namespace StorageService.Infrastructure.Encryption;

/// AES-256-GCM encryption service.
/// 
/// Encrypted format: [12-byte nonce][ciphertext][16-byte authentication tag]
/// 
/// - Nonce is randomly generated per encryption operation (never reused).
/// - Authentication tag ensures integrity and authenticity.
/// - The master key is loaded once from configuration at startup.

public class AesGcmEncryptionService : IEncryptionService
{
    private const int NonceSize = 12;  // AES-GCM standard nonce size
    private const int TagSize = 16;    // AES-GCM standard tag size
    private const int KeySize = 32;    // 256-bit key

    private readonly byte[] _masterKey;
    private readonly ILogger<AesGcmEncryptionService> _logger;

    public AesGcmEncryptionService(IOptions<EncryptionSettings> options, ILogger<AesGcmEncryptionService> logger)
    {
        _logger = logger;
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.MasterKeyBase64))
            throw new InvalidOperationException("Encryption is enabled but ENCRYPTION_MASTER_KEY is not configured.");

        _masterKey = Convert.FromBase64String(settings.MasterKeyBase64);

        if (_masterKey.Length != KeySize)
            throw new InvalidOperationException($"Encryption master key must be exactly {KeySize} bytes (256-bit). Got {_masterKey.Length} bytes.");

        _logger.LogInformation("AES-256-GCM encryption service initialized");
    }

    public async Task<Stream> EncryptAsync(Stream plaintext, CancellationToken ct = default)
    {
        // Read the entire plaintext into memory
        using var plaintextMs = new MemoryStream();
        await plaintext.CopyToAsync(plaintextMs, ct);
        var plaintextBytes = plaintextMs.ToArray();

        // Generate a random nonce for this operation
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);

        // Prepare output buffers
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        // Encrypt
        using var aesGcm = new AesGcm(_masterKey, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Assemble output
        var output = new MemoryStream();
        await output.WriteAsync(nonce, ct);
        await output.WriteAsync(ciphertext, ct);
        await output.WriteAsync(tag, ct);
        output.Position = 0;

        _logger.LogDebug("Encrypted {PlaintextSize} bytes → {EncryptedSize} bytes",
            plaintextBytes.Length, output.Length);

        return output;
    }

    public async Task<Stream> DecryptAsync(Stream ciphertext, CancellationToken ct = default)
    {
        // Read the entire ciphertext into memory
        using var ciphertextMs = new MemoryStream();
        await ciphertext.CopyToAsync(ciphertextMs, ct);
        var allBytes = ciphertextMs.ToArray();

        if (allBytes.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted data is too short to contain nonce and authentication tag.");

        // Extract: [nonce][ciphertext][tag]
        var nonce = allBytes[..NonceSize];
        var encryptedData = allBytes[NonceSize..^TagSize];
        var tag = allBytes[^TagSize..];

        // Decrypt
        var plaintext = new byte[encryptedData.Length];
        using var aesGcm = new AesGcm(_masterKey, TagSize);
        aesGcm.Decrypt(nonce, encryptedData, tag, plaintext);

        var output = new MemoryStream(plaintext);
        output.Position = 0;

        _logger.LogDebug("Decrypted {EncryptedSize} bytes → {PlaintextSize} bytes",
            allBytes.Length, plaintext.Length);

        return output;
    }
}
