using ApiGateway.Protos;
using Grpc.Core;
using System.Security;

namespace API_Gateway.Services
{
    public interface IPermissionService
    {
        Task<CheckRoleIdAndPermissionResponse?> CheckRoleIdAndPermission(int id, string permissionName);
    }
    public class PermissionService : IPermissionService
    {
        private readonly Permission.PermissionClient _client;
        private readonly ILogger<PermissionService> _logger;

        public PermissionService(Permission.PermissionClient client, ILogger<PermissionService> logger)
        {
            _client = client; _logger = logger;
        }

        public async Task<CheckRoleIdAndPermissionResponse?> CheckRoleIdAndPermission(int id, string permissionName)
        {
            try
            {
                return await _client.CheckRoleIdAndPermissionAsync(new CheckRoleIdAndPermissionRequest { RoleId = id, PermissionName = permissionName }).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while checking role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
        }
    }
}
