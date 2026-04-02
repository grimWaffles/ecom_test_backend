using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Security.Claims;
using System.Threading.Tasks;

namespace API_Gateway.Controllers
{
    [ApiController]
    [Route("api/users")]
    [EnableCors("AllowOrigin")]
    public class UserController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;
        public UserController(IUserServiceClient userService)
        {
            _userServiceClient = userService;
        }

        [HttpGet]
        [Route("test")]
        public async Task<IActionResult> TestUserService()
        {
            string response = "";// await _userServiceClient.TestServiceAsync();
            return StatusCode(StatusCodes.Status200OK, new { message = response });
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> LoginUser([FromForm] string username, [FromForm] string password)
        {
            UserLoginResponse response = await _userServiceClient.LoginUserAsync(username, password);
            if (response == null || response.UserId == 0)
            {
                return Unauthorized();
            }

            return StatusCode(StatusCodes.Status200OK, new { data = response, message = "Login Successful" });
        }

        [HttpPost]
        [Route("logout")]
        public async Task<IActionResult> LogoutUsers([FromForm] int userId)
        {
            var response = await _userServiceClient.LogoutUserAsync(userId);
            if (response == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { data = response });
            }

            return StatusCode(StatusCodes.Status200OK, new { data = response });
        }

        [HttpGet]
        [Route("get-all-users")]
        [Authorize]
        public async Task<IActionResult> GetAllUsers()
        {
            List<CreateUserRequest> responseMultiple = await _userServiceClient.GetAllUsersAsync();

            return StatusCode(StatusCodes.Status200OK, new { data = responseMultiple, message = "" });
        }

        // GET api/users/getById?userId=5
        [HttpGet("getById")]
        [Authorize]
        public async Task<IActionResult> GetUserById([FromQuery] int userId)
        {
            try
            {
                var result = await _userServiceClient.GetUserByIdAsync(userId);
                if (result != null)
                    return StatusCode(200, result);

                return StatusCode(500, "User not found or null response");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // POST api/users/create
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateUser([FromForm] CreateUserRequest user)
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;

                // If not found, try common alternatives
                if (string.IsNullOrEmpty(userId))
                {
                    userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    user.UserId = Convert.ToInt32(userId);
                }

                var result = await _userServiceClient.CreateUserAsync(user);
                if (result != null)
                    return StatusCode(200, result);

                return StatusCode(500, "Failed to create user");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // PUT api/users/update
        [HttpPut("update")]
        [Authorize]
        public async Task<IActionResult> UpdateUser([FromForm] CreateUserRequest user)
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;

                // If not found, try common alternatives
                if (string.IsNullOrEmpty(userId))
                {
                    userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    user.UserId = Convert.ToInt32(userId);
                }

                var result = await _userServiceClient.UpdateUserAsync(user);
                if (result != null)
                    return StatusCode(200, result);

                return StatusCode(500, "Failed to update user");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // DELETE api/users/delete
        [HttpDelete("delete")]
        [Authorize]
        public async Task<IActionResult> DeleteUser([FromForm] int id)
        {
            try
            {
                var userId = User.FindFirst("userId")?.Value;

                // If not found, try common alternatives
                if (string.IsNullOrEmpty(userId))
                    userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var result = await _userServiceClient.DeleteUserAsync(id,Convert.ToInt32(userId));
                if (result != null)
                    return StatusCode(200, result);

                return StatusCode(500, "Failed to delete user");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
