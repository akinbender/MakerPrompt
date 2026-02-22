using BlazorBootstrap;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class ConnectionComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject]
        public required MakerPromptJsInterop JS { get; set; }

        [Inject]
        public required IStringLocalizer<Resources> Localizer { get; set; }

        [Inject]
        public required PrinterCommunicationServiceFactory PrinterServiceFactory { get; set; }

        [Inject]
        private ILogger<ConnectionComponentBase> Logger { get; set; } = null!;

        [Inject]
        private ToastService ToastService { get; set; } = null!;

        protected bool IsConnected { get; set; }
        protected string ConnectionDisabledAttribute => IsConnected ? string.Empty : "disabled";

        /// <summary>
        /// Executes a printer command, catches any exception, logs it, and surfaces
        /// a toast — so subclasses never need individual try/catch blocks.
        /// </summary>
        protected async Task RunAsync(Func<Task> action, string errorTitle = "Command failed")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Printer command failed: {Title}", errorTitle);
                ToastService.Notify(new ToastMessage(ToastType.Danger, errorTitle, ex.Message));
            }
        }

        private void OnTelemetryUpdated(object? sender, PrinterTelemetry e)
        {
            HandleTelemetryUpdated(sender, e);
            InvokeAsync(StateHasChanged);
        }

        protected override void OnInitialized()
        {
            IsConnected = PrinterServiceFactory.IsConnected;
            PrinterServiceFactory.ConnectionStateChanged += HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated += OnTelemetryUpdated;
            }

            base.OnInitialized();
        }

        protected virtual void HandleTelemetryUpdated(object? sender, PrinterTelemetry printerTelemetry) { }

        protected virtual void HandleConnectionChanged(object? sender, bool connected)
        {
            IsConnected = connected;
            if (PrinterServiceFactory.Current != null)
            {
                if (IsConnected)
                {
                    PrinterServiceFactory.Current.TelemetryUpdated += OnTelemetryUpdated;
                }
                else
                {
                    PrinterServiceFactory.Current.TelemetryUpdated -= OnTelemetryUpdated;
                }
            }
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            PrinterServiceFactory.ConnectionStateChanged -= HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated -= OnTelemetryUpdated;
            }
            GC.SuppressFinalize(this);
        }
    }
}