
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace UserServiceGrpc.Services
{
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
        bool SetValueByKey(string keyName, string keyValue);
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

        public bool SetValueByKey(string keyName, string keyValue)
        {
            try
            {
                _redis.StringSet(keyName, keyValue);
                return true;
            }
            catch (Exception e)
            {
                return false;
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