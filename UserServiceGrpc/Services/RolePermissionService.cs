using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface IRolePermissionService
    {
        Task<List<RolePermissionDto>> GetAllPermissionsByRoleId(long roleId);
        Task<RolePermissionDto> CreateRolePermission(RolePermissionDto model, int userId);
        Task<RolePermissionDto> UpdateRolePermission(RolePermissionDto model, int userId);
        Task DeleteRolePermission(long id, int userId);
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
