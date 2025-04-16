namespace MakerPrompt.Shared.Models
{
    public class PrinterConnectionSettings
    {
        public PrinterConnectionType ConnectionType { get; set; }

        public SerialConnectionSettings? Serial { get; set; }

        public ApiConnectionSettings? Api { get; set; }

        public PrinterConnectionSettings(SerialConnectionSettings serialConnectionSettings)
        {
            ConnectionType = PrinterConnectionType.Serial;
            Serial = serialConnectionSettings;
        }

        public PrinterConnectionSettings(ApiConnectionSettings apiConnectionSettings, PrinterConnectionType connectionType)
        {
            if (connectionType == PrinterConnectionType.Serial) throw new ArgumentOutOfRangeException(nameof(connectionType));
            ConnectionType = connectionType;
            Api = apiConnectionSettings;
        }
    }

    public record SerialConnectionSettings
    {
        public string PortName { get; set; } = string.Empty;

        public int BaudRate { get; set; } = 115200;
    }

    public class ApiConnectionSettings
    {
        public ApiConnectionSettings()
        {
        }
        public ApiConnectionSettings(string url, string username, string password)
        {
            Url = url;
            UserName = username;
            Password = password;
        }
        public string Url { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
