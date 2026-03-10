using System.Globalization;
using MakerPrompt.Blazor;
using MakerPrompt.Blazor.Services;
using MakerPrompt.Blazor.Storage;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, WebSerialService>();
builder.Services.AddScoped<MakerPrompt.Shared.Infrastructure.IAppLocalStorageProvider, BlazorAppLocalStorageProvider>();
// WASM: AES-GCM not supported in browser — use Base64 encoding fallback
builder.Services.AddSingleton<IConnectionEncryptionService, Base64ConnectionEncryptionService>();

var host = builder.Build();
const string defaultCulture = "en-US";

// Initialize configuration from localStorage before the app renders so that
// Index.razor and other components see the persisted FarmModeEnabled / FarmName
// values on the very first render rather than always defaulting.
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