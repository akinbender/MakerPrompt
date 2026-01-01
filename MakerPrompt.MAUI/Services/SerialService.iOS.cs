using System.IO.Ports;
using System.Threading.Tasks.Dataflow;
using MakerPrompt.Shared.Infrastructure;
using System.Text;
using MakerPrompt.Shared.Models;

namespace MakerPrompt.MAUI.Services
{
    public class SerialService : BaseSerialService, ISerialService
    {
        public bool IsSupported => false;

        public SerialService()
        {

        }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings) => throw new NotSupportedException();
        public async Task DisconnectAsync() => throw new NotSupportedException();
        public override async Task WriteDataAsync(string data) => throw new NotSupportedException();
        public async Task<IEnumerable<string>> GetAvailablePortsAsync() => throw new NotSupportedException();

        public override async ValueTask DisposeAsync()
        {
        }

        public Task<bool> CheckSupportedAsync() => Task.FromResult(false);

        public Task RequestPortAsync()  => throw new NotSupportedException();
    }
}