namespace MakerPrompt.UI.Components.Utils
{
    public class SerialException(string message, Exception inner) : Exception(message, inner)
    {
    }
}
