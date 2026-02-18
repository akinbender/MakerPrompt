namespace MakerPrompt.Shared.Infrastructure
{
    public interface IAppStorage
    {
        Task<T?> GetItemAsync<T>(string key);
        Task SetItemAsync<T>(string key, T value);
        Task RemoveItemAsync(string key);
    }
}
