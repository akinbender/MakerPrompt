using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MakerPrompt.Cloud.Authentication;

/// <summary>
/// Authenticates EdgeAgent bearer tokens against a configured SHA-256 digest.
/// The digest allows the Cloud host to avoid storing the pre-shared token.
/// </summary>
public sealed class AgentApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "EdgeAgentApiKey";
    public const string IngestScope = "makerprompt:ingest";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredHash = configuration["MakerPrompt:Auth:AgentApiKeyHash"];
        if (string.IsNullOrWhiteSpace(configuredHash))
        {
            configuredHash = configuration["CloudApi:AgentApiKeyHash"];
        }

        if (string.IsNullOrWhiteSpace(configuredHash))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!TryDecodeSha256(configuredHash, out var expectedHash))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "The configured EdgeAgent API-key hash is invalid."));
        }

        if (!AuthenticationHeaderValue.TryParse(
                Request.Headers.Authorization.FirstOrDefault(),
                out var authorization) ||
            !authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization.Parameter;
        if (token.Length > 4096)
        {
            return Task.FromResult(AuthenticateResult.Fail("The bearer token is invalid."));
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            return Task.FromResult(AuthenticateResult.Fail("The bearer token is invalid."));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "edge-agent"),
            new Claim("scope", IngestScope),
        ], SchemeName);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    internal static bool TryDecodeSha256(string value, out byte[] hash)
    {
        hash = [];
        var normalized = value.Trim();
        if (normalized.Length != 64)
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(normalized);
            return hash.Length == SHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
