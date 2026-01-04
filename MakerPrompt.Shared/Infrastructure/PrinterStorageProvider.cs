namespace MakerPrompt.Shared.Infrastructure
{
    using MakerPrompt.Shared.Models;

    public sealed class PrinterStorageProvider : IStorageProvider
    {
        private readonly PrinterCommunicationServiceFactory factory;
        public PrinterStorageProvider(PrinterCommunicationServiceFactory factory)
        {
            this.factory = factory;
        }

        public string DisplayName => factory.Current?.ConnectionName ?? "Printer";
        public string Key => "printer";

        public async Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            var svc = factory.Current;
            if (svc == null) return [];
            return await svc.GetFilesAsync() ?? [];
        }

        public async Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            if (factory.Current is Services.DemoPrinterService svc)
            {
                return await svc.OpenReadAsync(fullPath);
            }
            return null;
        }

        public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
        {
            if (factory.Current is Services.DemoPrinterService svc)
            {
                await svc.SaveFileAsync(fullPath, content);
            }
        }

        public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            if (factory.Current is Services.DemoPrinterService svc)
            {
                await svc.DeleteFileAsync(fullPath);
            }
        }
    }
}
