using API_Gateway.Helpers;
using API_Gateway.Services;
using API_Gateway.Redis;
using ApiGateway.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace API_Gateway.AuthHandlers.Handlers
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
        private readonly IPermissionService _permissionService;
        private readonly ILogger<RoleAuthorizationHandler> _logger;

        public RoleAuthorizationHandler(IPermissionService permissionService, ILogger<RoleAuthorizationHandler> logger)
        {
            _permissionService = permissionService;
            _logger = logger;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RolePermissionRequirement requirement)
        {
            try
            {
                _logger.LogInformation("Handling authorization for permission {Permission}", requirement.Permission);

                var roleIdClaim = context.User.FindFirst("roleId");

                if (roleIdClaim == null || !int.TryParse(roleIdClaim.Value, out int roleId) || roleId <= 0)
                {
                    _logger.LogWarning("Invalid or missing roleId claim");
                    context.Fail();
                    return;
                }

                bool isPermitted = await _permissionService.CheckRoleIdAndPermission(roleId, requirement.Permission);

                if (!isPermitted)
                {
                    _logger.LogWarning("Authorization failed for roleId {RoleId} and permission {Permission}", roleId, requirement.Permission);

                    context.Fail();
                    return;
                }

                context.Succeed(requirement);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Permission service RPC failure");
                context.Fail();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authorization handler failure");
                context.Fail();
            }
        }
    }
}
