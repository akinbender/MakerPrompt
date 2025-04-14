namespace MakerPrompt.Shared.Models
{
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
