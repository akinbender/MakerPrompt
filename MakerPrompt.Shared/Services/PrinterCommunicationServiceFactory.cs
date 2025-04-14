using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Services
{
    public class PrinterCommunicationServiceFactory(
        ISerialService serialService)
    {
        public event EventHandler<bool>? ConnectionStateChanged;
        public bool IsConnected { get; private set; }
        public IPrinterCommunicationService? Current { get; private set; }

        private readonly ISerialService serialService = serialService;

        public async Task ConnectSerialAsync(string portName, int baudRate)
        {
            if (serialService.IsSupported == false) return;

            if (Current != null && Current.ConnectionType != PrinterConnectionType.Serial)
            {
                await Current.DisposeAsync();
            }

            if (await serialService.ConnectAsync(portName, baudRate))
            {
                Current = serialService;
                IsConnected = Current.IsConnected;
                ConnectionStateChanged?.Invoke(this, IsConnected);
            }
        }

        public async Task ConnectPrusaLinkAsync(ApiConnectionSettings connectionSettings) => 
            await TryConnectAsync(new PrusaLinkApiService(connectionSettings));

        public async Task ConnectMoonrakerAsync(ApiConnectionSettings connectionSettings) => 
            await TryConnectAsync(new MoonrakerApiService(connectionSettings));

        public async Task DisconnectAsync()
        {
            if (Current == null) return;
            await Current.DisconnectAsync();
            IsConnected = Current.IsConnected;
            ConnectionStateChanged?.Invoke(this, IsConnected);
        }

        private async Task TryConnectAsync(IPrinterCommunicationService service)
        {
            if (Current != null && Current.ConnectionType != service.ConnectionType)
            {
                await Current.DisposeAsync();
            }

            if (await service.ConnectAsync())
            {
                Current = service;
                IsConnected = Current.IsConnected;
                ConnectionStateChanged?.Invoke(this, IsConnected);
            }
        }
    }
}
