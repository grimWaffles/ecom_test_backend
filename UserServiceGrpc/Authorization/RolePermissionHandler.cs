using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Services;

namespace UserServiceGrpc.Authorization
{
    public class RolePermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; set; }

        public RolePermissionRequirement(string permission)
        {
            this.Permission = permission;
        }
    }

    public class RolePermissionHandler : AuthorizationHandler<RolePermissionRequirement>
    {
        private readonly ILogger<RolePermissionHandler> _logger;
        private readonly ITokenHelper _tokenHelper;
        private readonly IRolePermissionService _permissionService;

        public RolePermissionHandler(ILogger<RolePermissionHandler> logger, ITokenHelper tokenHelper, IRolePermissionService permissionService)
        {
            _logger = logger;
            _tokenHelper = tokenHelper;
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RolePermissionRequirement requirement)
        {
            try
            {
                _logger.LogInformation("Handling RoleAuth Requirement for requirement: {requirement}", requirement);

                string roleId = _tokenHelper.GetClaimValueFromToken("RoleId");
                string permissionName = _tokenHelper.GetClaimValueFromToken("Permission");


                if (string.IsNullOrWhiteSpace(roleId) || string.IsNullOrEmpty(permissionName))
                {
                    context.Fail();
                    _logger.LogError("Permission Name and/or roleId not found");
                    return;
                }

                if (permissionName != requirement.Permission)
                {
                    context.Fail();
                    _logger.LogError("User does not have matching permission");
                    return;
                }

                //Replace this with a cache call after Redis is setup
                bool isAuthorized = await _permissionService.CheckRoleIdAndPermissionName(long.Parse(roleId), requirement.Permission);

                if (!isAuthorized)
                {
                    _logger.LogCritical("Unauthorized user detected for requirement: {r}", requirement);
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
