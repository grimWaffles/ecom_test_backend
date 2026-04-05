using API_Gateway.Services.API_Gateway.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace API_Gateway.Handlers
{
    public class RolePermissionRequirement : IAuthorizationRequirement 
    {

    }
    public class RolePermissionAuthorizationHandler : AuthorizationHandler<RolePermissionRequirement>
    {
        private readonly IUserService _userService;
        private readonly ILogger<RolePermissionAuthorizationHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RolePermissionAuthorizationHandler(IUserService service, ILogger<RolePermissionAuthorizationHandler> logger, IHttpContextAccessor accessor)
        {
            _userService = service;
            _logger = logger;
            _httpContextAccessor = accessor;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RolePermissionRequirement requirement)
        {
            try
            {
                _logger.LogInformation($"Authorization is handled for path: {_httpContextAccessor.HttpContext.Request.Path} and method {_httpContextAccessor.HttpContext.Request.Method}");
                context.Succeed(requirement);
            }
            catch(RpcException e)
            {
                context.Fail();
            }
        }
    }
}
