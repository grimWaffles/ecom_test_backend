using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface IRolePermissionService
    {
        Task<RolePermissionDto> GetPermissionByRoleIdAsync(int roleId);
        Task<RolePermissionDto> GetPermissionByIdAsync(int id);
        Task<RolePermissionDto> GetPermissionByNameAsync(string permissionName);
        Task<RolePermissionDto> CreateAsync(RolePermissionDto dto, int userId);
        Task<RolePermissionDto> UpdateAsync(RolePermissionDto dto, int userId);
        Task<bool> SoftDeleteAsync(int id, int userId);
    }

    public class RolePermissionService : IRolePermissionService
    {
        private readonly IRolePermissionRepository _repository;
        private readonly ILogger<RolePermissionService> _logger;

        public RolePermissionService(IRolePermissionRepository repository, ILogger<RolePermissionService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        // ── Mappers ──────────────────────────────────────────────────────────

        private static RolePermission ToModel(RolePermissionDto dto) => new RolePermission
        {
            Id = dto.Id,
            RoleId = dto.RoleId,
            PermissionId = dto.PermissionId
        };

        private static RolePermissionDto ToDto(RolePermission model) => new RolePermissionDto
        {
            Id = model.Id,
            RoleId = model.RoleId,
            PermissionId = model.PermissionId,
            RoleName = model.Role?.Name,
            PermissionName = model.Permission?.Permission
        };

        // ── Read operations ──────────────────────────────────────────────────

        public async Task<RolePermissionDto> GetPermissionByRoleIdAsync(int roleId)
        {
            try
            {
                _logger.LogInformation("Fetching permission for RoleId: {RoleId}", roleId);
                var result = await _repository.GetPermissionByRoleIdAsync(roleId);
                if (result == null)
                {
                    _logger.LogWarning("No permission found for RoleId: {RoleId}", roleId);
                    return null;
                }
                return ToDto(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while fetching permission for RoleId: {RoleId}", roleId);
                throw;
            }
        }

        public async Task<RolePermissionDto> GetPermissionByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Fetching permission with Id: {Id}", id);
                var result = await _repository.GetPermissionByIdAsync(id);
                if (result == null)
                {
                    _logger.LogWarning("No permission found with Id: {Id}", id);
                    return null;
                }
                return ToDto(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while fetching permission with Id: {Id}", id);
                throw;
            }
        }

        public async Task<RolePermissionDto> GetPermissionByNameAsync(string permissionName)
        {
            try
            {
                _logger.LogInformation("Fetching permission with Name: {PermissionName}", permissionName);
                var result = await _repository.GetPermissionByNameAsync(permissionName);
                if (result == null)
                {
                    _logger.LogWarning("No permission found with Name: {PermissionName}", permissionName);
                    return null;
                }
                return ToDto(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while fetching permission with Name: {PermissionName}", permissionName);
                throw;
            }
        }

        // ── Write operations ─────────────────────────────────────────────────

        public async Task<RolePermissionDto> CreateAsync(RolePermissionDto dto, int userId)
        {
            try
            {
                _logger.LogInformation("Creating RolePermission for RoleId: {RoleId} by UserId: {UserId}", dto.RoleId, userId);
                var model = ToModel(dto);
                var result = await _repository.CreateAsync(model, userId);
                return ToDto(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while creating RolePermission for RoleId: {RoleId}", dto.RoleId);
                throw;
            }
        }

        public async Task<RolePermissionDto> UpdateAsync(RolePermissionDto dto, int userId)
        {
            try
            {
                _logger.LogInformation("Updating RolePermission with Id: {Id} by UserId: {UserId}", dto.Id, userId);
                var model = ToModel(dto);
                var result = await _repository.UpdateAsync(model, userId);
                return ToDto(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while updating RolePermission with Id: {Id}", dto.Id);
                throw;
            }
        }

        public async Task<bool> SoftDeleteAsync(int id, int userId)
        {
            try
            {
                _logger.LogInformation("Soft deleting RolePermission with Id: {Id} by UserId: {UserId}", id, userId);
                var success = await _repository.SoftDeleteAsync(id, userId);
                if (!success)
                    _logger.LogWarning("RolePermission with Id: {Id} not found for soft delete", id);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in service while soft deleting RolePermission with Id: {Id}", id);
                throw;
            }
        }
    }
}
