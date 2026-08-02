
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Net;
using System.Text;

namespace API_Gateway.Services
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
        Task<string> GetValueByKey(string key);
        Task<string> SetValueByKey(string keyName, string keyValue);
        bool DoesKeyExist(string key);
        bool DeleteKey(string key);
        Task<List<RedisKeyInfo>> ListAllKeys();
    }
    public class RedisService : IRedisService
    {
        private readonly IOptions<RedisConfigModel> _config;
        private readonly IDatabase _redis;
        private readonly ILogger<RedisService> _logger;
        private readonly ConnectionMultiplexer _redisConnector;

        public RedisService(IOptions<RedisConfigModel> config, ILogger<RedisService> logger)
        {
            _logger = logger;

            _config = config;

            ConfigurationOptions configurationOptions = new ConfigurationOptions()
            {
                EndPoints = { _config.Value.GetRedisConnectionString() },
                User = _config.Value.Username,
                Password = _config.Value.Password
            };

            _redisConnector = ConnectionMultiplexer.Connect(configurationOptions);

            _redis = _redisConnector.GetDatabase();
            _logger.LogInformation("Redis service up and running!");
        }

        public async Task<string> GetValueByKey(string key)
        {
            try
            {
                return await _redis.StringGetAsync(key);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public async Task<string> SetValueByKey(string keyName, string keyValue)
        {
            try
            {
                bool keyAdded = await _redis.StringSetAsync(keyName, keyValue);
                return keyAdded ? "Success" : "Failed";
            }
            catch (Exception e)
            {
                return "Failed";
            }
        }

        public bool DoesKeyExist(string key)
        {
            return _redis.KeyExists(key);
        }

        public bool DeleteKey(string key)
        {
            if (_redis.KeyExists(key))
            {
                _redis.KeyDelete(key);
                return true;
            }

            return false;
        }

        public async Task<List<RedisKeyInfo>> ListAllKeys()
        {
            try
            {
                List<RedisKeyInfo> list = new List<RedisKeyInfo>();

                EndPoint endPoints = _redisConnector.GetEndPoints().First();
                IServer[] redisServer = _redisConnector.GetServers();

                foreach (var key in redisServer[0].Keys())
                {
                    var type = await _redis.KeyTypeAsync(key);
                    var ttl = await _redis.KeyTimeToLiveAsync(key);

                    list.Add(new RedisKeyInfo
                    {
                        Key = key!,
                        Type = type.ToString(),
                        Ttl = ttl,
                        Value = await ReadValueAsync(key, type)
                    });
                }

                return list.OrderBy(x=>x.Key).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to fetch key information");
                return null;
            }
        }

        private async Task<string> ReadValueAsync(RedisKey key,  RedisType type)
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
                            sb.Append($"{entry.Id}: ");

                            sb.AppendJoin(
                                ", ",
                                entry.Values.Select(v => $"{v.Name}={v.Value}"));

                            sb.AppendLine();
                        }

                        return sb.ToString();
                    }

                default:
                    return "<Unsupported Type>";
            }
        }
    }
}