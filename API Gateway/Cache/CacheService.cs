using API_Gateway.Services;
using Microsoft.Extensions.Caching.Memory;

namespace API_Gateway.CacheService
{
    public interface ICustomCacheService
    {
        T? Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan expiration);
        void Remove(string key);
    }

    public class CustomCacheService : ICustomCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomCacheService> _logger;

        public CustomCacheService(
            IMemoryCache cache,
            ILogger<CustomCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public T? Get<T>(string key)
        {
            if (_cache.TryGetValue(key, out T? value))
            {
                _logger.LogInformation(
                    "Cache HIT for key: {CacheKey}",
                    key);

                return value;
            }

            _logger.LogInformation(
                "Cache MISS for key: {CacheKey}",
                key);

            return default;
        }

        public void Set<T>(
            string key,
            T value,
            TimeSpan expiration)
        {
            _cache.Set(key, value, expiration);

            _logger.LogInformation(
                "Cache SET for key: {CacheKey}, Expiration: {ExpirationMinutes} minutes",
                key,
                expiration.TotalMinutes);
        }

        public void Remove(string key)
        {
            _cache.Remove(key);

            _logger.LogInformation(
                "Cache REMOVE for key: {CacheKey}",
                key);
        }
    }
}
