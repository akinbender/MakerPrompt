namespace MakerPrompt.Shared.Models
{
    public sealed class PrinterConnectionDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Printer";
        public PrinterConnectionType PrinterType { get; set; } = PrinterConnectionType.Demo;
        public string Address { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SerialPortName { get; set; } = string.Empty;
        public int BaudRate { get; set; } = 115200;
        public bool Enabled { get; set; } = true;
        public bool AutoConnect { get; set; }

        public PrinterConnectionSettings ToSettings()
        {
            return PrinterType switch
            {
                PrinterConnectionType.Serial => new PrinterConnectionSettings(new SerialConnectionSettings
                {
                    PortName = SerialPortName,
                    BaudRate = BaudRate
                }),
                PrinterConnectionType.PrusaLink or PrinterConnectionType.Moonraker or PrinterConnectionType.BambuLab =>
                    new PrinterConnectionSettings(new ApiConnectionSettings(Address, UserName, Password), PrinterType),
                _ => new PrinterConnectionSettings()
            };
        }

        public static PrinterConnectionDefinition FromSettings(PrinterConnectionSettings settings, string? name = null)
        {
            var definition = new PrinterConnectionDefinition
            {
                Name = name ?? settings.ConnectionType.GetDisplayName(),
                PrinterType = settings.ConnectionType,
                Enabled = true,
                AutoConnect = false
            };

            if (settings.Serial is not null)
            {
                definition.SerialPortName = settings.Serial.PortName;
                definition.BaudRate = settings.Serial.BaudRate;
            }

            if (settings.Api is not null)
            {
                definition.Address = settings.Api.Url;
                definition.UserName = settings.Api.UserName;
                definition.Password = settings.Api.Password;
            }

            return definition;
        }
    }
}
