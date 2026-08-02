
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using System.Text;

namespace API_Gateway.Redis
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

    public class RedisKeyInfo
    {
        public string Key { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public TimeSpan? Ttl { get; init; }
        public string Value { get; init; } = string.Empty;

        public override string ToString()
        {
            var ttl = Ttl.HasValue
                ? $"{Ttl.Value.TotalSeconds:N0}s"
                : "No Expiry";

            return $"""
                Key   : {Key}
                Type  : {Type}
                TTL   : {ttl}
                Value : {Value}
                """;
        }
    }

    public interface IRedisService
    {
        Task<string?> GetValueByKeyAsync(string key);

        Task<bool> SetValueByKeyAsync( string key, string value, TimeSpan? ttl = null, DateTime? expireAt = null, bool randomizeTtl = false);

        Task<bool> DoesKeyExistAsync(string key);

        Task<bool> DeleteKeyAsync(string key);

        Task<bool> AcquireLockAsync( string lockKey, string lockValue, TimeSpan expiry);

        Task<bool> ReleaseLockAsync( string lockKey, string lockValue);

        Task<List<RedisKeyInfo>> ListAllKeys();
    }

    public class RedisService : IRedisService
    {
        private readonly IDatabase _redis;
        private readonly IConnectionMultiplexer _redisConnection;
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

        public RedisService(IConnectionMultiplexer redisConn, IDatabase redisDB, ILogger<RedisService> logger)
        {
            _logger = logger;
            _redis = redisDB;
            _redisConnection = redisConn;

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

        public async Task<bool> SetValueByKeyAsync(string key, string value, TimeSpan? ttl = null, DateTime? expireAt = null, bool randomizeTtl = false)
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

        public async Task<bool> AcquireLockAsync(string lockKey, string lockValue, TimeSpan expiry)
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

        public async Task<bool> ReleaseLockAsync(string lockKey, string lockValue)
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

        public async Task<List<RedisKeyInfo>> ListAllKeys()
        {
            List<RedisKeyInfo> result = new List<RedisKeyInfo>();

            try
            {
                EndPoint[] redisEndpoint = _redisConnection.GetEndPoints();

                if (redisEndpoint == null || redisEndpoint.Length == 0)
                {
                    return result;
                }

                var redisServer = _redisConnection.GetServer(redisEndpoint[0]);

                if (!redisServer.IsConnected)
                {
                    _logger.LogError("Redis server not connected at {r}", redisServer.EndPoint);
                }

                foreach (var key in redisServer.Keys())
                {
                    var type = await _redis.KeyTypeAsync(key);
                    var ttl = await _redis.KeyTimeToLiveAsync(key);

                    result.Add(new RedisKeyInfo
                    {
                        Key = key!,
                        Type = type.ToString(),
                        Ttl = ttl,
                        Value = await ReadValueAsync(key, type)
                    });
                }
            }
            catch (Exception e)
            {
                return new List<RedisKeyInfo>();
            }

            return result;
        }

        private async Task<string> ReadValueAsync(RedisKey key, RedisType type)
        {
            switch (type)
            {
                case RedisType.String:
                    return await _redis.StringGetAsync(key);

                case RedisType.Hash:
                    {
                        var values = await _redis.HashGetAllAsync(key);

                        return string.Join(
                            ", ",
                            values.Select(x => $"{x.Name}={x.Value}"));
                    }

                case RedisType.List:
                    {
                        var values = await _redis.ListRangeAsync(key);

                        return "[" + string.Join(", ", values) + "]";
                    }

                case RedisType.Set:
                    {
                        var values = await _redis.SetMembersAsync(key);

                        return "[" + string.Join(", ", values) + "]";
                    }

                case RedisType.SortedSet:
                    {
                        var values = await _redis.SortedSetRangeByRankWithScoresAsync(key);

                        return string.Join(
                            ", ",
                            values.Select(x => $"{x.Element} ({x.Score})"));
                    }

                case RedisType.Stream:
                    {
                        var entries = await _redis.StreamRangeAsync(key);

                        var sb = new StringBuilder();

                        foreach (var entry in entries)
                        {
                            sb.Append(entry.Id);
                            sb.Append(": ");

                            sb.AppendJoin(
                                ", ",
                                entry.Values.Select(v => $"{v.Name}={v.Value}"));

                            sb.AppendLine();
                        }

                        return sb.ToString();
                    }

                default:
                    return "<Unsupported>";
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