namespace MakerPrompt.UI.Components.Models;

/// <summary>
/// UI form-binding model for serial/USB connection settings.
/// Used to collect port name and baud rate before constructing a
/// <see cref="MakerPrompt.Core.Models.PrinterConnectionSettings"/>.
/// </summary>
public record SerialConnectionSettings
{
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115_200;
}

/// <summary>
/// UI form-binding model for HTTP/WebSocket API connection settings.
/// Used to collect URL, username, and password before constructing a
/// <see cref="MakerPrompt.Core.Models.PrinterConnectionSettings"/>.
/// </summary>
public class ApiConnectionSettings
{
    public ApiConnectionSettings() { }

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
