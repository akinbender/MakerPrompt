namespace MakerPrompt.Shared.Utils
{
    public class SerialException(string message, Exception inner) : Exception(message, inner)
    {
    }
}
