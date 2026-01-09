using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.MAUI.Storage
{
    public sealed class MauiAppLocalStorageProvider : IAppLocalStorageProvider
    {
        public MauiAppLocalStorageProvider()
        {
#if ANDROID || IOS || MACCATALYST
            RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MakerPrompt", "LocalFiles");
#else
            RootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MakerPrompt", "LocalFiles");
#endif
            Directory.CreateDirectory(RootPath);
        }

        public string DisplayName => "App storage";
        public string Key => "app";
        public string RootPath { get; }

        public Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            var files = Directory.EnumerateFiles(RootPath, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new FileEntry
                {
                    FullPath = path,
                    Size = new FileInfo(path).Length,
                    ModifiedDate = File.GetLastWriteTime(path),
                    IsAvailable = true
                }).ToList();
            return Task.FromResult(files);
        }

        public Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            try
            {
                var fs = File.OpenRead(fullPath);
                return Task.FromResult<Stream?>(fs);
            }
            catch
            {
                return Task.FromResult<Stream?>(null);
            }
        }

        public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
        {
            var path = MapToLocal(fullPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = File.Create(path);
            await content.CopyToAsync(fs, cancellationToken);
        }

        public Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            var path = MapToLocal(fullPath);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        private string MapToLocal(string fullPath)
        {
            var name = Path.GetFileName(fullPath);
            return Path.Combine(RootPath, name);
        }
    }
}
