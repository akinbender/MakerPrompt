namespace MakerPrompt.UI.MAUI;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        Content = new BlazorWebView
        {
            HostPage = "wwwroot/index.html",
            RootComponents =
            {
                new RootComponent
                {
                    Selector = "#app",
                    ComponentType = typeof(MakerPrompt.UI.Components.App)
                }
            }
        };
    }
}
