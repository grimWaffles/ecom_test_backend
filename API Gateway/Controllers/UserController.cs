using API_Gateway.AuthHandlers;
using API_Gateway.Models;
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
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IUserService _userServiceClient;
        private readonly IAuthorizationService _authorizationService;
        public UserController(IUserService userService, IAuthorizationService authorizationService, IHttpContextAccessor contextAccessor)
        {
            _userServiceClient = userService;
            _authorizationService = authorizationService;
            _contextAccessor = contextAccessor;
        }

        [HttpGet]
        [Route("test")]
        [AllowAnonymous]
        public async Task<IActionResult> TestUserService()
        {
            string response = await _userServiceClient.TestServiceAsync();
            return StatusCode(StatusCodes.Status200OK, new { message = response });
        }
         
        [HttpPost]
        [Route("login")]
        [AllowAnonymous]
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
        [AllowAnonymous]
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
        [RequiresPermission("user.view")]
        public async Task<IActionResult> GetAllUsers()
        {
            List<CreateUserRequest> responseMultiple = await _userServiceClient.GetAllUsersAsync();

            return StatusCode(StatusCodes.Status200OK, new { data = responseMultiple, message = "" });
        }

        // GET api/users/getById?userId=5
        [HttpGet("getById")]
        [RequiresPermission("user.view")]
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
        [RequiresPermission("user.create")]
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
        [RequiresPermission("user.update")]
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
        [RequiresPermission("user.delete")]
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

        [HttpGet("role/test/permission")]
        [RequiresPermission("cart.read")]
        public async Task<IActionResult> TestRolePermissionAccess()
        {
            return Ok();
        }

        [HttpGet("role/test/permission/2")]
        [RequiresPermission("order.create")]
        public async Task<IActionResult> TestRolePermissionAccess2()
        {
            return Ok();
        }

        [HttpGet("role/test/admin-plain")]
        [Authorize]
        public async Task<IActionResult> TestPlainAuthTagAccess()
        {
            return Ok();
        }

        [HttpGet("role/test/admin-req")]
        //[Authorize(Policy = "AdminOnly")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> TestRoleAccess()
        {
            return Ok();
        }
    }
}
