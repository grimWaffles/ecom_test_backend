
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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

    public interface IRedisService
    {
        Task<string> GetValueByKey(string key);
        string SetValueByKey(string keyName, string keyValue);
        bool DoesKeyExist(string key);
        bool DeleteKey(string key);
    }
    public class RedisService : IRedisService
    {
        private readonly IOptions<RedisConfigModel> _config;
        private readonly IDatabase _redis;
        private readonly ILogger<RedisService> _logger;

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

            ConnectionMultiplexer redisConnector = ConnectionMultiplexer.Connect(configurationOptions);

            _redis = redisConnector.GetDatabase();
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

        public string SetValueByKey(string keyName, string keyValue)
        {
            try
            {
                bool keyAdded = _redis.StringSet(keyName, keyValue);
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
    }
}