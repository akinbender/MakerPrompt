using System.Security.Cryptography;
using System.Runtime.Versioning;

namespace MakerPrompt.Shared.Services
{
    [UnsupportedOSPlatform("browser")]
    public sealed class AesGcmAppDataProtectionService : IAppDataProtectionService
    {
        private readonly IAppEncryptionKeyStore _keyStore;

        public AesGcmAppDataProtectionService(IAppEncryptionKeyStore keyStore)
        {
            _keyStore = keyStore;
        }

        public async Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
        {
            var baseKey = await _keyStore.GetOrCreateKeyAsync(cancellationToken);
            var encryptionKey = DeriveKey(baseKey, "enc");
            var macKey = DeriveKey(baseKey, "mac");

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] iv;
            byte[] ciphertextBytes;

            using (var aes = Aes.Create())
            {
                aes.Key = encryptionKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                iv = aes.IV;
                using var encryptor = aes.CreateEncryptor();
                ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
            }

            var mac = ComputeMac(macKey, iv, ciphertextBytes);

            var envelope = new EncryptedEnvelope
            {
                Version = 1,
                Iv = Convert.ToBase64String(iv),
                Mac = Convert.ToBase64String(mac),
                CipherText = Convert.ToBase64String(ciphertextBytes)
            };

            return JsonSerializer.Serialize(envelope);
        }

        public async Task<string?> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default)
        {
            EncryptedEnvelope? envelope;

            try
            {
                envelope = JsonSerializer.Deserialize<EncryptedEnvelope>(ciphertext);
            }
            catch
            {
                return null;
            }

            if (envelope is null || envelope.Version != 1)
            {
                return null;
            }

            byte[] iv;
            byte[] mac;
            byte[] encrypted;

            try
            {
                iv = Convert.FromBase64String(envelope.Iv);
                mac = Convert.FromBase64String(envelope.Mac);
                encrypted = Convert.FromBase64String(envelope.CipherText);
            }
            catch
            {
                return null;
            }

            var baseKey = await _keyStore.GetOrCreateKeyAsync(cancellationToken);
            var encryptionKey = DeriveKey(baseKey, "enc");
            var macKey = DeriveKey(baseKey, "mac");

            var expectedMac = ComputeMac(macKey, iv, encrypted);
            if (!CryptographicOperations.FixedTimeEquals(mac, expectedMac))
            {
                return null;
            }

            try
            {
                using var aes = Aes.Create();
                aes.Key = encryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                var plaintext = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                return Encoding.UTF8.GetString(plaintext);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DeriveKey(byte[] baseKey, string purpose)
        {
            var purposeBytes = Encoding.UTF8.GetBytes(purpose);
            var material = new byte[baseKey.Length + purposeBytes.Length];
            Buffer.BlockCopy(baseKey, 0, material, 0, baseKey.Length);
            Buffer.BlockCopy(purposeBytes, 0, material, baseKey.Length, purposeBytes.Length);
            return SHA256.HashData(material);
        }

        private static byte[] ComputeMac(byte[] macKey, byte[] iv, byte[] ciphertext)
        {
            var payload = new byte[iv.Length + ciphertext.Length];
            Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
            Buffer.BlockCopy(ciphertext, 0, payload, iv.Length, ciphertext.Length);

            using var hmac = new HMACSHA256(macKey);
            return hmac.ComputeHash(payload);
        }

        private sealed class EncryptedEnvelope
        {
            public int Version { get; set; }
            public string Iv { get; set; } = string.Empty;
            public string Mac { get; set; } = string.Empty;
            public string CipherText { get; set; } = string.Empty;
        }
    }
}
