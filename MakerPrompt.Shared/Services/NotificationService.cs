using Microsoft.Extensions.Logging;
using BlazorBootstrap;

namespace MakerPrompt.Shared.Services
{
    public class NotificationService
    {
        private const string StorageKey = "MakerPrompt.Notifications.json";
        private readonly IAppLocalStorageProvider _storage;
        private readonly ToastService _toastService;
        private readonly ILogger<NotificationService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<NotificationRecord> _notifications = [];

        public event EventHandler? NotificationsChanged;

        public NotificationService(IAppLocalStorageProvider storage, ToastService toastService, ILogger<NotificationService> logger)
        {
            _storage = storage;
            _toastService = toastService;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var files = await _storage.ListFilesAsync();
                var file = files.FirstOrDefault(f => f.FullPath.Contains(StorageKey));
                if (file != null)
                {
                    using var stream = await _storage.OpenReadAsync(file.FullPath);
                    if (stream != null)
                    {
                        using var reader = new StreamReader(stream);
                        var json = await reader.ReadToEndAsync();
                        var stored = JsonSerializer.Deserialize<List<NotificationRecord>>(json);
                        if (stored != null)
                        {
                            _notifications = stored;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load notifications");
            }
            finally
            {
                _lock.Release();
            }
        }

        public IReadOnlyList<NotificationRecord> GetNotifications() => _notifications.AsReadOnly();

        public async Task NotifyAsync(NotificationLevel level, string title, string message, Guid? printerId = null, Guid? filamentSpoolId = null)
        {
            var record = new NotificationRecord
            {
                Level = level,
                Title = title,
                Message = message,
                PrinterId = printerId,
                FilamentSpoolId = filamentSpoolId
            };

            await _lock.WaitAsync();
            try
            {
                _notifications.Add(record);
                var json = JsonSerializer.Serialize(_notifications);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                await _storage.SaveFileAsync(StorageKey, stream);
            }
            finally
            {
                _lock.Release();
            }

            var toastType = level switch
            {
                NotificationLevel.Info => ToastType.Info,
                NotificationLevel.Warning => ToastType.Warning,
                NotificationLevel.Error => ToastType.Danger,
                NotificationLevel.Critical => ToastType.Danger,
                _ => ToastType.Info
            };

            _toastService.Notify(new ToastMessage(toastType, title, message));
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task MarkAsReadAsync(Guid id)
        {
            await _lock.WaitAsync();
            try
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == id);
                if (notification != null)
                {
                    notification.IsRead = true;
                    var json = JsonSerializer.Serialize(_notifications);
                    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
                    await _storage.SaveFileAsync(StorageKey, stream);
                }
            }
            finally
            {
                _lock.Release();
            }
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
