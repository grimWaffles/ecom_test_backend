
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace OrderServiceGrpc.Services
{
    public class RedisKeyValueModel
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class RedisConfigModel
    {
        public const string SectionName = "Redis";
        public string LocalUrl { get; set; }
        public string DockerUrl { get; set; }
        public string Mode { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public string GetRedisConnectionString() => this.Mode == "docker" ? this.DockerUrl : this.LocalUrl;
    }

    public interface IRedisService
    {
        Task<string?> GetValueByKeyAsync(string key);

        Task<bool> SetValueByKeyAsync(
            string key,
            string value,
            TimeSpan? ttl = null,
            DateTime? expireAt = null,
            bool randomizeTtl = false);

        Task<bool> DoesKeyExistAsync(string key);

        Task<bool> DeleteKeyAsync(string key);

        Task<bool> AcquireLockAsync(
            string lockKey,
            string lockValue,
            TimeSpan expiry);

        Task<bool> ReleaseLockAsync(
            string lockKey,
            string lockValue);
    }

    public class RedisService : IRedisService
    {
        private readonly IDatabase _redis;
        private readonly ILogger<RedisService> _logger;
        private static readonly Random _random = new();
        private const string RELEASE_LOCK_SCRIPT =
            """
            if redis.call('GET', KEYS[1]) == ARGV[1]
            then
                return redis.call('DEL', KEYS[1])
            else
                return 0
            end
            """;

        public RedisService(IDatabase redisDB, ILogger<RedisService> logger)
        {
            _logger = logger;
            _redis = redisDB;
            
            _logger.LogInformation("Redis service up and running!");
        }

        public async Task<string?> GetValueByKeyAsync(string key)
        {
            try
            {
                RedisValue value = await _redis.StringGetAsync(key);

                return value.IsNull ? null : value.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to get redis key {Key}",
                    key);

                return null;
            }
        }

        public async Task<bool> SetValueByKeyAsync(
            string key,
            string value,
            TimeSpan? ttl = null,
            DateTime? expireAt = null,
            bool randomizeTtl = false)
        {
            try
            {
                TimeSpan? expiry = null;

                if (expireAt.HasValue)
                {
                    expiry = expireAt.Value - DateTime.UtcNow;
                }
                else if (ttl.HasValue)
                {
                    expiry = ttl.Value;

                    if (randomizeTtl)
                    {
                        expiry = AddRandomization(expiry.Value);
                    }
                }

                return await _redis.StringSetAsync(
                    key,
                    value,
                    (Expiration)expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to set redis key {Key}",
                    key);

                return false;
            }
        }

        public async Task<bool> DoesKeyExistAsync(string key)
        {
            try
            {
                return await _redis.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to check redis key {Key}",
                    key);

                return false;
            }
        }

        public async Task<bool> DeleteKeyAsync(string key)
        {
            try
            {
                return await _redis.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete redis key {Key}",
                    key);

                return false;
            }
        }

        public async Task<bool> AcquireLockAsync(
            string lockKey,
            string lockValue,
            TimeSpan expiry)
        {
            try
            {
                return await _redis.StringSetAsync(
                    lockKey,
                    lockValue,
                    expiry,
                    When.NotExists);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to acquire lock {LockKey}",
                    lockKey);

                return false;
            }
        }

        public async Task<bool> ReleaseLockAsync(
            string lockKey,
            string lockValue)
        {
            try
            {
                var result = await _redis.ScriptEvaluateAsync(
                    RELEASE_LOCK_SCRIPT,
                    new RedisKey[] { lockKey },
                    new RedisValue[] { lockValue });

                return (int)result == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to release lock {LockKey}",
                    lockKey);

                return false;
            }
        }

        private static TimeSpan AddRandomization(TimeSpan ttl)
        {
            int jitterPercent = _random.Next(5, 16); // 5-15%

            return ttl.Add(
                TimeSpan.FromMilliseconds(
                    ttl.TotalMilliseconds * jitterPercent / 100.0));
        }
    }
}