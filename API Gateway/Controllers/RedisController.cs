using System.Text.Json;
using System.Threading.Tasks;
using API_Gateway.Redis;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Mvc;

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
            await _redisService.SetValueByKeyAsync(model.Key, model.Value);
            return Ok("Addtion complete");
        }

        [HttpGet]
        [Route("get-key")]
        public async Task<IActionResult> GetKey([FromQuery] string? key)
        {
            if (key != "permissions")
            {
                string value = await _redisService.GetValueByKeyAsync(key) ?? "";
                return Ok(key + ": " + value);
            }
            else
            {
                if (await _redisService.DoesKeyExistAsync(key))
                {
                    var redisList = await _redisService.GetValueByKeyAsync(key);
                    List<RolePermissionDto> list = JsonSerializer.Deserialize<List<RolePermissionDto>>(redisList)?? new List<RolePermissionDto>();
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
            if (await _redisService.DoesKeyExistAsync(key))
            {
                await _redisService.DeleteKeyAsync(key);
                return Ok("Data deleted");
            }
            return Ok("No key exists.");
        }
    }
}