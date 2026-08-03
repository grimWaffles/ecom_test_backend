using Grpc.Core;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public interface IPermissionService
    {
        Task<bool> CheckRoleIdAndPermission(int roleId, string permissionName);
    }
    public class PermissionService : IPermissionService
    {
        private readonly ILogger<PermissionService> _logger;
        private readonly IRedisService _redis;

        public PermissionService(ILogger<PermissionService> logger, IRedisService redisService)
        {
            _logger = logger;
            _redis = redisService;
        }

        public async Task<bool> CheckRoleIdAndPermission(int roleId, string permissionName)
        {
            try
            {
                bool exists = false;

                //Check redis first
                string permissionKey = $"permission:{roleId}:{permissionName}";
                string? permissionValue = await _redis.GetValueByKeyAsync(permissionKey);

                if (permissionValue == null)
                {
                    _logger.LogWarning("Cache Miss: Permission not found for ROLE: {role} and PERMISSION: {p}. Checking DB", roleId, permissionName);
                }

                if (permissionValue != null)
                {
                    // parse safely instead of converting a possible null
                    exists = permissionValue == "1";
                    _logger.LogInformation("Cache Hit: Found permission for ROLE: {role} and PERMISSION: {p}", roleId, permissionName);
                    return exists;
                }

                //on failing check the DB
                _logger.LogWarning("Cache Miss: Found permission in DB for ROLE: {role} and PERMISSION: {p}", roleId, permissionName);
                
                return exists;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}, {permission}", roleId, permissionName);
                return false;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while checking role permission by id, name: {Id}, {permission}", roleId, permissionName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking role permission by id, name: {Id}, {permission}", roleId, permissionName);
                return false;
            }
        }
    }
}
