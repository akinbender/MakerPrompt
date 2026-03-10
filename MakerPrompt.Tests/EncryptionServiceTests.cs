using MakerPrompt.Shared.Services;
using System.Security.Cryptography;

namespace MakerPrompt.Tests;

public class EncryptionServiceTests
{
    // ── Base64ConnectionEncryptionService ──

    [Fact]
    public void Base64_Encrypt_Then_Decrypt_RoundTrip()
    {
        var svc = new Base64ConnectionEncryptionService();
        const string plain = "supersecret-api-key-12345";
        var cipher = svc.Encrypt(plain);
        var decrypted = svc.Decrypt(cipher);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Base64_Encrypt_EmptyString_ReturnsEmpty()
    {
        var svc = new Base64ConnectionEncryptionService();
        Assert.Equal(string.Empty, svc.Encrypt(string.Empty));
    }

    [Fact]
    public void Base64_Decrypt_EmptyString_ReturnsEmpty()
    {
        var svc = new Base64ConnectionEncryptionService();
        Assert.Equal(string.Empty, svc.Decrypt(string.Empty));
    }

    [Fact]
    public void Base64_Decrypt_InvalidBase64_ReturnsOriginalString()
    {
        var svc = new Base64ConnectionEncryptionService();
        const string notBase64 = "this-is-not-base64!!!";
        var result = svc.Decrypt(notBase64);
        Assert.Equal(notBase64, result);
    }

    [Fact]
    public void Base64_Encrypt_ProducesBase64EncodedOutput()
    {
        var svc = new Base64ConnectionEncryptionService();
        var cipher = svc.Encrypt("hello");
        // Should be valid base64
        var _ = Convert.FromBase64String(cipher);  // throws if not valid base64
    }

    [Fact]
    public void Base64_RoundTrip_SpecialCharacters()
    {
        var svc = new Base64ConnectionEncryptionService();
        const string plain = "url:https://printer.local:7125/api?key=abc&secret=xyz!@#$";
        Assert.Equal(plain, svc.Decrypt(svc.Encrypt(plain)));
    }

    // ── AesConnectionEncryptionService ──
    // Shared instance to amortize the one-time PBKDF2 key derivation cost.
    private static readonly AesConnectionEncryptionService _aes =
        new("unit-test-device-identifier-stable");

    [Fact]
    public void Aes_Encrypt_Then_Decrypt_RoundTrip()
    {
        const string plain = "my-secret-api-key";
        var cipher = _aes.Encrypt(plain);
        var decrypted = _aes.Decrypt(cipher);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Aes_Encrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _aes.Encrypt(string.Empty));
    }

    [Fact]
    public void Aes_Decrypt_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _aes.Decrypt(string.Empty));
    }

    [Fact]
    public void Aes_Encrypt_ProducesValidBase64()
    {
        var cipher = _aes.Encrypt("test");
        var _ = Convert.FromBase64String(cipher);  // throws if invalid
    }

    [Fact]
    public void Aes_Encrypt_SamePlaintext_ProducesDifferentCiphers_EachTime()
    {
        // AES-GCM uses a random nonce per call — same plaintext must not produce same ciphertext
        var c1 = _aes.Encrypt("same-value");
        var c2 = _aes.Encrypt("same-value");
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void Aes_Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var cipher = _aes.Encrypt("sensitive-data");
        var bytes = Convert.FromBase64String(cipher);
        bytes[^1] ^= 0xFF;  // corrupt last byte (ciphertext area) — breaks the GCM tag
        var tampered = Convert.ToBase64String(bytes);
        Assert.ThrowsAny<CryptographicException>(() => _aes.Decrypt(tampered));
    }

    [Fact]
    public void Aes_Constructor_EmptyDeviceId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AesConnectionEncryptionService(string.Empty));
    }
}
