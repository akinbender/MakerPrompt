using System.Security.Cryptography;

namespace MakerPrompt.UI.Components.Services;

/// <summary>
/// Protects credentials embedded in persisted printer definitions. Runtime models stay
/// decrypted; callers must clone definitions before protecting them.
/// </summary>
public sealed class PrinterConnectionDefinitionProtector(IConnectionEncryptionService encryption)
{
    public const string ProtectedPrefix = "enc:v1:";

    public void Protect(PrinterConnectionDefinition definition)
    {
        var settings = definition.Settings;
        settings.UserName = ProtectValue(settings.UserName);
        settings.Password = ProtectValue(settings.Password);
    }

    public void Unprotect(PrinterConnectionDefinition definition, bool allowLegacyUnmarked = false)
    {
        var settings = definition.Settings;
        settings.UserName = UnprotectValue(settings.UserName, allowLegacyUnmarked);
        settings.Password = UnprotectValue(settings.Password, allowLegacyUnmarked);
    }

    public static void Redact(PrinterConnectionDefinition definition)
    {
        definition.Settings.UserName = string.Empty;
        definition.Settings.Password = string.Empty;
    }

    public static bool IsProtected(string? value) =>
        value?.StartsWith(ProtectedPrefix, StringComparison.Ordinal) == true;

    private string? ProtectValue(string? value)
    {
        if (string.IsNullOrEmpty(value) || IsProtected(value))
            return value;

        return ProtectedPrefix + encryption.Encrypt(value);
    }

    private string? UnprotectValue(string? value, bool allowLegacyUnmarked)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (IsProtected(value))
        {
            var payload = value[ProtectedPrefix.Length..];
            if (payload.Length == 0)
                return value;

            try
            {
                var decrypted = encryption.Decrypt(payload);
                return string.Equals(decrypted, payload, StringComparison.Ordinal)
                    ? value
                    : decrypted;
            }
            catch (Exception ex) when (ex is ArgumentException or CryptographicException)
            {
                return value;
            }
        }

        if (!allowLegacyUnmarked)
            return value;

        try
        {
            return encryption.Decrypt(value);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            return value;
        }
    }
}
