
using API_Gateway.Services;
using ApiGateway.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace API_Gateway.Handlers
{
    public class RolePermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; set; }

        public RolePermissionRequirement(string permission)
        {
            this.Permission = permission;
        }
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
                _logger.LogInformation("Handling RoleAuth Requirement for requirement: {requirement}", requirement);

                Claim roleClaim = context.User.FindFirst("role") ?? null;

                if (roleClaim is null)
                {
                    context.Fail();
                    return;
                }

                context.Succeed(requirement);
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
