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
            if (svc == null) return new();
            return await svc.GetFilesAsync() ?? new();
        }

        public async Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            var svc = factory.Current as Services.DemoPrinterService;
            if (svc != null)
            {
                return await svc.OpenReadAsync(fullPath);
            }
            return null;
        }

        public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
        {
            var svc = factory.Current as Services.DemoPrinterService;
            if (svc != null)
            {
                await svc.SaveFileAsync(fullPath, content);
            }
        }

        public async Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            var svc = factory.Current as Services.DemoPrinterService;
            if (svc != null)
            {
                await svc.DeleteFileAsync(fullPath);
            }
        }
    }
}
