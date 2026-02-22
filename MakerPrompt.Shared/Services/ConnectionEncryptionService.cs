using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace MakerPrompt.Shared.Services
{
    /// <summary>
    /// Provides AES-256-GCM encryption for sensitive connection data (API keys, passwords).
    /// The key is derived from a device-stable identifier via PBKDF2.
    /// </summary>
    public interface IConnectionEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }

    /// <summary>
    /// AES-256-GCM implementation. Uses a salt + nonce prepended to the ciphertext.
    /// Format: Base64( salt[16] | nonce[12] | tag[16] | ciphertext )
    /// Only used on native platforms (MAUI). Browser/WASM uses Base64ConnectionEncryptionService.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    public sealed class AesConnectionEncryptionService : IConnectionEncryptionService
    {
        private const int SaltSize = 16;
        private const int NonceSize = 12;  // AES-GCM standard
        private const int TagSize = 16;    // AES-GCM standard
        private const int KeySize = 32;    // 256-bit
        private const int Iterations = 100_000;

        private readonly byte[] _masterKey;

        public AesConnectionEncryptionService(string deviceIdentifier)
        {
            if (string.IsNullOrWhiteSpace(deviceIdentifier))
                throw new ArgumentException("Device identifier is required for encryption key derivation.", nameof(deviceIdentifier));

            // Derive a stable master key from the device identifier
            // We use a fixed application salt so the same device always produces the same key
            var appSalt = "MakerPrompt.ConnectionStore.v1"u8.ToArray();
            _masterKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(deviceIdentifier),
                appSalt,
                Iterations,
                HashAlgorithmName.SHA256,
                KeySize);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);

            // Derive per-message key from master key + salt
            var key = Rfc2898DeriveBytes.Pbkdf2(_masterKey, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            var cipherText = new byte[plainBytes.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plainBytes, cipherText, tag);

            // Pack: salt | nonce | tag | ciphertext
            var result = new byte[SaltSize + NonceSize + TagSize + cipherText.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltSize);
            Buffer.BlockCopy(nonce, 0, result, SaltSize, NonceSize);
            Buffer.BlockCopy(tag, 0, result, SaltSize + NonceSize, TagSize);
            Buffer.BlockCopy(cipherText, 0, result, SaltSize + NonceSize + TagSize, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;

            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(cipherText);
            }
            catch (FormatException)
            {
                // If it's not valid Base64, return as-is (plaintext migration path)
                return cipherText;
            }

            if (packed.Length < SaltSize + NonceSize + TagSize)
            {
                // Too short to be encrypted — return as-is for backward compatibility
                return cipherText;
            }

            var salt = new byte[SaltSize];
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var encryptedBytes = new byte[packed.Length - SaltSize - NonceSize - TagSize];

            Buffer.BlockCopy(packed, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(packed, SaltSize, nonce, 0, NonceSize);
            Buffer.BlockCopy(packed, SaltSize + NonceSize, tag, 0, TagSize);
            Buffer.BlockCopy(packed, SaltSize + NonceSize + TagSize, encryptedBytes, 0, encryptedBytes.Length);

            var key = Rfc2898DeriveBytes.Pbkdf2(_masterKey, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            var plainBytes = new byte[encryptedBytes.Length];
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, encryptedBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }

    /// <summary>
    /// No-op encryption for platforms that don't support AES-GCM (e.g. Blazor WASM browser).
    /// Uses Base64 encoding only — not secure, but prevents casual reading.
    /// </summary>
    public sealed class Base64ConnectionEncryptionService : IConnectionEncryptionService
    {
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return string.Empty;
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
            }
            catch (FormatException)
            {
                return cipherText;
            }
        }
    }
}
