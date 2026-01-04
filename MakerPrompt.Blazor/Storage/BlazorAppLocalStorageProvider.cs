using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Infrastructure;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MakerPrompt.Blazor.Storage
{
    public sealed class BlazorAppLocalStorageProvider : IAppLocalStorageProvider
    {
        private const string ManifestKey = "MakerPrompt.LocalFiles.Manifest";
        private const string FilePrefix = "MakerPrompt.LocalFiles.File.";
        private readonly IJSRuntime js;

        public BlazorAppLocalStorageProvider(IJSRuntime jsRuntime)
        {
            js = jsRuntime;
        }

        public string DisplayName => "App storage";
        public string Key => "app";
        public string RootPath => "localStorage://MakerPrompt/LocalFiles";

        public async Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            var json = await js.InvokeAsync<string>("localStorage.getItem", ManifestKey);
            var entries = string.IsNullOrEmpty(json) ? [] : (JsonSerializer.Deserialize<List<ManifestEntry>>(json) ?? []);
            return entries.Select(e => new FileEntry
            {
                FullPath = e.Name,
                Size = e.Size,
                ModifiedDate = e.Modified,
                IsAvailable = true
            }).ToList();
        }

        public async Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            var key = FilePrefix + fullPath;
            var base64 = await js.InvokeAsync<string>("localStorage.getItem", key);
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                return new MemoryStream(bytes);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            var bytes = ms.ToArray();
            var base64 = Convert.ToBase64String(bytes);
            var key = FilePrefix + fullPath;
            await js.InvokeVoidAsync("localStorage.setItem", key, base64);

            var manifest = await GetManifestAsync();
            var existing = manifest.FirstOrDefault(m => m.Name == fullPath);
            var now = DateTime.Now;
            if (existing != null)
            {
                existing.Size = bytes.Length;
                existing.Modified = now;
            }
            else
            {
                manifest.Add(new ManifestEntry { Name = fullPath, Size = bytes.Length, Modified = now });
            }
            await SaveManifestAsync(manifest);
        }

        public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            var key = FilePrefix + fullPath;
            await js.InvokeVoidAsync("localStorage.removeItem", key);
            var manifest = await GetManifestAsync();
            manifest = manifest.Where(m => m.Name != fullPath).ToList();
            await SaveManifestAsync(manifest);
        }

        private async Task<List<ManifestEntry>> GetManifestAsync()
        {
            var json = await js.InvokeAsync<string>("localStorage.getItem", ManifestKey);
            return string.IsNullOrEmpty(json) ? [] : (JsonSerializer.Deserialize<List<ManifestEntry>>(json) ?? []);
        }

        private Task SaveManifestAsync(List<ManifestEntry> entries)
        {
            var json = JsonSerializer.Serialize(entries);
            return js.InvokeVoidAsync("localStorage.setItem", ManifestKey, json).AsTask();
        }

        private sealed class ManifestEntry
        {
            public string Name { get; set; } = string.Empty;
            public int Size { get; set; }
            public DateTime? Modified { get; set; }
        }
    }
}
