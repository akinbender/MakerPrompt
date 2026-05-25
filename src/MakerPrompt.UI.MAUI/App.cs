namespace MakerPrompt.UI.MAUI;

public class App : Microsoft.Maui.Controls.Application
{
    public App()
    {
    }

    protected override Microsoft.Maui.Controls.Window CreateWindow(Microsoft.Maui.IActivationState? activationState)
    {
        return new Microsoft.Maui.Controls.Window(new MainPage()) { Title = "MakerPrompt" };
    }
}
