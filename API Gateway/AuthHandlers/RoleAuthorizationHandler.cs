using API_Gateway.Services.API_Gateway.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace API_Gateway.Handlers
{
    public class RolePermissionRequirement : IAuthorizationRequirement
    {

    }
    public class RoleAuthorizationHandler : AuthorizationHandler<RolePermissionRequirement>
    {
        private readonly IUserService _userService;
        private readonly ILogger<RoleAuthorizationHandler> _logger;

        public RoleAuthorizationHandler(IUserService service, ILogger<RoleAuthorizationHandler> logger)
        {
            _userService = service;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RolePermissionRequirement requirement)
        {
            try
            {
                if (context.Resource is HttpContext http)
                {
                    if (http.User.Identity.IsAuthenticated)
                    {
                        var userClaims = http.User.Claims;

                        string entity = http.Request.Path;
                        int userId = Convert.ToInt32(userClaims?.FirstOrDefault(c => c?.Type?.ToLower() == "userid").Value);
                        int roleId = Convert.ToInt32(userClaims?.FirstOrDefault(c => c?.Type?.ToLower() == "roleid").Value);

                        _logger.LogInformation("Authorization is handled for path: {path} and method {method}", http.Request.Path, http.Request.Method);

                        context.Succeed(requirement);
                        return;
                    }
                }

                context.Fail();
            }
            catch (RpcException e)
            {
                _logger.LogError("Auth Handler RPC Error: {error}. Stacktrace: {stacktrace}", e.Message, e.StackTrace);
                context.Fail();
            }
            catch (Exception e)
            {
                _logger.LogError("Auth Handler Error: {error}. Stacktrace: {stacktrace}", e.Message, e.StackTrace);
                context.Fail();
            }
        }
    }
}
