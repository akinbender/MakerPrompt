namespace MakerPrompt.Shared.Infrastructure
{
    public interface ISerialService : IPrinterCommunicationService
    {
        bool IsSupported { get; }

        Task<IEnumerable<string>> GetAvailablePortsAsync();

        Task<bool> CheckSupportedAsync();
        Task RequestPortAsync();

        Task<bool> ConnectAsync(string portName, int baudRate);
        //Task OpenPortAsync(string port, int baudRate, int dataBits = 8, int stopBits = 1,
        //    string parity = "none", string flowControl = "none");
    }
}
