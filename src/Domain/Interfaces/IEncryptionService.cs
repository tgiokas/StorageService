namespace StorageService.Domain.Interfaces;

public interface IEncryptionService
{
    /// Encrypts the given plaintext stream and returns the ciphertext stream.
    /// The returned stream contains: [12-byte nonce][ciphertext][16-byte auth tag]
    Task<Stream> EncryptAsync(Stream plaintext, CancellationToken ct = default);

    /// Decrypts the given ciphertext stream and returns the plaintext stream.
    /// Expects format: [12-byte nonce][ciphertext][16-byte auth tag]
    Task<Stream> DecryptAsync(Stream ciphertext, CancellationToken ct = default);
}
