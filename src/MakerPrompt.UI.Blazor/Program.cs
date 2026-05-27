using System.Globalization;
using MakerPrompt.UI.Blazor.Services;
using MakerPrompt.UI.Blazor.Storage;
using MakerPrompt.UI.Components.Infrastructure;
using MakerPrompt.UI.Components.Services;
using MakerPrompt.Infrastructure.Printers;
using MakerPrompt.UI.Components.Utils;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<MakerPrompt.UI.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, WebSerialService>();
builder.Services.AddScoped<IAppLocalStorageProvider, BlazorAppLocalStorageProvider>();
// WASM: AES-GCM not supported in browser — use Base64 encoding fallback
builder.Services.AddSingleton<IConnectionEncryptionService, Base64ConnectionEncryptionService>();

// ── Deployment-mode-specific services ────────────────────────────────────────
// OIDC is enabled when an Authority URL is present in the "Local" config section.
// This drives IDeploymentModeService.IsAuthEnabled and locks the farm in
// cloud/makerspace mode.  Config lives in wwwroot/appsettings.json under "Local".
var oidcAuthority = builder.Configuration["Local:Authority"] ?? string.Empty;
if (!string.IsNullOrWhiteSpace(oidcAuthority))
{
    // Authorization Code + PKCE (never implicit grant).
    // All options (ClientId, ResponseType, DefaultScopes, etc.) are bound from
    // the "Local" section, which follows the Microsoft.AspNetCore.Components
    // .WebAssembly.Authentication convention.
    builder.Services.AddOidcAuthentication(options =>
    {
        builder.Configuration.Bind("Local", options.ProviderOptions);
        // Enforce PKCE / code flow explicitly.
        options.ProviderOptions.ResponseType = "code";
    });
}

var host = builder.Build();
const string defaultCulture = "en-US";

// Initialize configuration from localStorage before the app renders so that
// components see the persisted values on the very first render.
var configService = host.Services.GetRequiredService<IAppConfigurationService>();
await configService.InitializeAsync();

var js = host.Services.GetRequiredService<IJSRuntime>();
var result = await js.InvokeAsync<string>("blazorCulture.get");
var culture = CultureInfo.GetCultureInfo(result ?? defaultCulture);

if (result == null)
{
    await js.InvokeVoidAsync("blazorCulture.set", defaultCulture);
}

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
