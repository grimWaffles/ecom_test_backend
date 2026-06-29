using System.Text.Json;
using System.Threading.Tasks;
using API_Gateway.Models.RedisModels;
using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace API_Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RedisController(IRedisService redisService) : ControllerBase
    {
        private readonly IRedisService _redisService = redisService;

        [HttpGet]
        [Route("test")]
        public IActionResult TestController()
        {
            return Ok("Controller Functional");
        }

        [HttpPost]
        [Route("add-key")]
        public async Task<IActionResult> AddKeyToRedis([FromBody] RedisKeyValueModel model)
        {
            _redisService.SetValueByKey(model.Key,model.Value);
            return Ok("Addtion complete");
        }

        [HttpGet]
        [Route("get-key")]
        public async Task<IActionResult> GetKey([FromQuery] string? key)
        {
            if (key != null)
            {
                string value = await _redisService.GetValueByKey(key);
                return Ok(key+": "+value);
            }
            else
            {
                string keyToAdd = "permissions";
                if (_redisService.DoesKeyExist(key))
                {
                    var redisList = await _redisService.GetValueByKey(keyToAdd);
                    List<RolePermissionDto> list = JsonSerializer.Deserialize<List<RolePermissionDto>>(redisList);
                    return Ok(list);
                }
                else
                {
                    return Ok("No key exists.");
                }
            }
        }

        [HttpGet]
        [Route("del-key")]
        public async Task<IActionResult> DeleteKey()
        {
            string key = "user_obj";
            if (_redisService.DoesKeyExist(key))
            {
                _redisService.DeleteKey(key);
                return Ok("Data deleted");
            }
            return Ok("No key exists.");
        }
    }
}