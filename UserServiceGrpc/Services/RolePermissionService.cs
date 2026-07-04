using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface IRolePermissionService
    {
        Task<List<RolePermissionDto>> GetAllPermissionsByRoleId(long roleId);
        Task<List<RolePermissionDto>> GetPermissionByRoleIdAndPermissionName(long roleId, string permissionName);
        Task<bool> CheckRoleIdAndPermissionName(long roleId, string permissionName);
        Task<RolePermissionDto> CreateRolePermission(RolePermissionDto model, int userId);
        Task<bool> DeleteRolePermission(long id, int userId);
        Task<Dictionary<string, string>> GetPermissionListDictionary(List<RolePermissionDto> dtos);
    }

    public class RolePermissionService : IRolePermissionService
    {
        private readonly ILogger<RolePermissionService> _logger;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly ISecurityPermissionService _spService;
        private readonly IRedisService _redis;

        public RolePermissionService(
            ILogger<RolePermissionService> logger, IRedisService redis, ISecurityPermissionService spService,
            IRolePermissionRepository rolePermissionRepository)
        {
            _logger = logger;
            _rolePermissionRepository = rolePermissionRepository;
            _spService = spService;
            _redis = redis;
        }

        public async Task<List<RolePermissionDto>> GetAllPermissionsByRoleId(long roleId)
        {
            try
            {
                List<RolePermission> list = await _rolePermissionRepository.GetAllPermissionsByRoleId(roleId);

                return list.Select(x => Mapper.CreateRolePermissionDtoFromModel(x)).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<Dictionary<string, string>> GetPermissionListDictionary(List<RolePermissionDto> dtos)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (RolePermissionDto dto in dtos)
            {
                string key = "permission:" + dto.RoleId.ToString() + ":" + dto.PermissionName;
                string value = 1.ToString();

                result.Add(key, value);
            }

            return result;
        }

        public async Task<List<RolePermissionDto>> GetPermissionByRoleIdAndPermissionName(long roleId, string permissionName)
        {
            try
            {
                List<RolePermission> list = await _rolePermissionRepository.GetPermissionByRoleIdAndPermissionName(roleId, permissionName);

                return list.Select(x => Mapper.CreateRolePermissionDtoFromModel(x)).ToList();
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<bool> CheckRoleIdAndPermissionName(long roleId, string permissionName)
        {
            try
            {
                bool exists = await _rolePermissionRepository.CheckRoleIdAndPermissionName(roleId, permissionName);

                return exists;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<RolePermissionDto> CreateRolePermission(RolePermissionDto model, int userId)
        {
            try
            {
                RolePermission created = await _rolePermissionRepository.CreateRolePermission(Mapper.CreateRolePermissionModelFromDto(model), userId);

                if (created == null)
                {
                    return null;
                }

                SecurityPermission permissionName = await _spService.GetByIdAsync(model.PermissionId) ?? new SecurityPermission();

                string keyName = "permission:" + model.RoleId + ":" + permissionName.Permission;
                await _redis.SetValueByKeyAsync(keyName, "1", TimeSpan.FromDays(30), null, true);

                return Mapper.CreateRolePermissionDtoFromModel(created);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to create role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<bool> DeleteRolePermission(long id, int userId)
        {
            try
            {
                //Check if exists
                RolePermission model = await _rolePermissionRepository.GetPermissionById(id);

                if (model == null || model.Id == 0)
                {
                    return false;
                }

                bool isDeleted = await _rolePermissionRepository.DeleteRolePermission(id, userId);

                if (isDeleted)
                {
                    //Update cache
                    SecurityPermission permissionName = await _spService.GetByIdAsync(model.PermissionId) ?? new SecurityPermission();

                    string keyName = "permission:" + model.RoleId + ":" + permissionName.Permission;
                    await _redis.SetValueByKeyAsync(keyName, "1", TimeSpan.FromDays(30), null, true);
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to delete role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }
    }
}
