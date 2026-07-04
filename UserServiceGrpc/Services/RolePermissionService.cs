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
        Task<RolePermissionDto> UpdateRolePermission(RolePermissionDto model, int userId);
        Task DeleteRolePermission(long id, int userId);
        Task<Dictionary<string, string>> GetPermissionListDictionary(List<RolePermissionDto> dtos);
    }

    public class RolePermissionService : IRolePermissionService
    {
        private readonly ILogger<RolePermissionService> _logger;
        private readonly IRolePermissionRepository _rolePermissionRepository;

        public RolePermissionService(
            ILogger<RolePermissionService> logger,
            IRolePermissionRepository rolePermissionRepository)
        {
            _logger = logger;
            _rolePermissionRepository = rolePermissionRepository;
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

        public async Task<Dictionary<string,string>> GetPermissionListDictionary(List<RolePermissionDto> dtos)
        {
            Dictionary<string,string> result = new Dictionary<string,string>();

            foreach(RolePermissionDto dto in dtos)
            {
                string key = "permission:"+dto.RoleId.ToString()+":"+dto.PermissionName;
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
                return created == null ? null : Mapper.CreateRolePermissionDtoFromModel(created);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to create role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<RolePermissionDto> UpdateRolePermission(RolePermissionDto model, int userId)
        {
            try
            {
                RolePermission updated = await _rolePermissionRepository.UpdateRolePermission(Mapper.CreateRolePermissionModelFromDto(model), userId);
                return updated == null ? null : Mapper.CreateRolePermissionDtoFromModel(updated);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to update role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task DeleteRolePermission(long id, int userId)
        {
            try
            {
                await _rolePermissionRepository.DeleteRolePermission(id, userId);
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to delete role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }
    }
}
