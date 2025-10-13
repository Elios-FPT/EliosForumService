using ForumService.Core.Interfaces;
using ZiggyCreatures.Caching.Fusion;

namespace ForumService.Infrastructure.Implementations
{
    public class AppCacheService : IAppCacheService
    {
        private readonly IFusionCache _fusionCache;

        public AppCacheService(IFusionCache fusionCache)
        {
            _fusionCache = fusionCache;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var result = await _fusionCache.TryGetAsync<T>(key);
            return result.HasValue ? result.Value : default;
        }


        public async Task SetAsync<T>(string key, T value)
        {
            await _fusionCache.SetAsync(key, value);
        }

        public async Task RemoveAsync(string key)
        {
            await _fusionCache.RemoveAsync(key);
        }
    }

}
