using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using UserServiceGrpc.Protos;
using UserServiceGrpc.Services;

namespace UserServiceGrpc.Grpc
{
    [AllowAnonymous]
    public class PermissionServiceGrpc : Permission.PermissionBase
    {
        private readonly ILogger<PermissionServiceGrpc> _logger;
        private readonly IRolePermissionService _rpService;

        public PermissionServiceGrpc(IRolePermissionService rolePermissionService, ILogger<PermissionServiceGrpc> logger)
        {
            _logger = logger;
            _rpService = rolePermissionService;
        }

        public override async Task<Protos.CheckRoleIdAndPermissionResponse> CheckRoleIdAndPermission(Protos.CheckRoleIdAndPermissionRequest request, ServerCallContext context)
        {
            bool result = await _rpService.CheckRoleIdAndPermissionName(request.RoleId, request.PermissionName);

            return new Protos.CheckRoleIdAndPermissionResponse() { Exists = result };
        }
    }
}
