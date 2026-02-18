using System.Globalization;
using MakerPrompt.Blazor;
using MakerPrompt.Blazor.Services;
using MakerPrompt.Blazor.Storage;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Utils;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.RegisterMakerPromptSharedServices<AppConfigurationService, WebSerialService>();
builder.Services.AddScoped<MakerPrompt.Shared.Infrastructure.IAppLocalStorageProvider, BlazorAppLocalStorageProvider>();
builder.Services.AddScoped<IAppEncryptionKeyStore, BlazorAppEncryptionKeyStore>();
builder.Services.AddScoped<IAppDataProtectionService, BlazorWebCryptoDataProtectionService>();

var host = builder.Build();
const string defaultCulture = "en-US";

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