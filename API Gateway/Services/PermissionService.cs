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
        private readonly IRedisService _redis;

        public PermissionService(Permission.PermissionClient client, ILogger<PermissionService> logger, IRedisService redisService)
        {
            _client = client; _logger = logger;
            _redis = redisService;

        }

        public async Task<CheckRoleIdAndPermissionResponse?> CheckRoleIdAndPermission(int roleId, string permissionName)
        {
            try
            {
                CheckRoleIdAndPermissionResponse exists = new CheckRoleIdAndPermissionResponse()
                {
                    Exists = false
                };

                //Check redis first
                string permissionKey = $"permission:{roleId}:{permissionName}";
                string permissionValue = await _redis.GetValueByKey(permissionKey);

                if (permissionValue == null)
                {
                    _logger.LogWarning("Cache Miss: Permission not found for ROLE: {role} and PERMISSION: {p}. Checking DB", roleId, permissionName);
                }

                if (permissionValue != null)
                {
                    exists.Exists = Convert.ToInt32(permissionValue) == 1 ? true : false;
                    _logger.LogInformation("Cache Hit: Found permission for ROLE: {role} and PERMISSION: {p}", roleId, permissionName);
                    return exists;
                }

                //on failing check the DB
                _logger.LogWarning("Cache Miss: Found permission in DB for ROLE: {role} and PERMISSION: {p}", roleId, permissionName);
                exists = await _client.CheckRoleIdAndPermissionAsync(new CheckRoleIdAndPermissionRequest { RoleId = roleId, PermissionName = permissionName }).ResponseAsync;

                return exists;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}, {permission}", roleId, permissionName);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while checking role permission by id, name: {Id}, {permission}", roleId, permissionName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking role permission by id, name: {Id}, {permission}", roleId, permissionName);
                return null;
            }
        }
    }
}
