namespace ForumService.Core.Interfaces
{
    public interface IAppCacheService
    {
        Task<T?> GetAsync<T>(string key);

        Task SetAsync<T>(string key, T value);

        Task RemoveAsync(string key);
    }
}
