using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository
{
    public interface IRolePermissionRepository
    {
        Task<List<RolePermission>> GetAllPermissionsByRoleId(long roleId);
        Task<RolePermission> CreateRolePermission(RolePermission model, int userId);
        Task<RolePermission> UpdateRolePermission(RolePermission model, int userId);
        Task DeleteRolePermission(long id, int userId);
    }

    public class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly ILogger<RolePermissionRepository> _logger;
        private readonly AppDbContext _db;

        public RolePermissionRepository(AppDbContext appDbContext, ILogger<RolePermissionRepository> logger)
        {
            _db = appDbContext;
            _logger = logger;
        }

        public async Task<List<RolePermission>> GetAllPermissionsByRoleId(long roleId)
        {
            try
            {
                return await _db.RolePermissions
                    .Include(x => x.Permission)
                    .Where(x => x.RoleId == roleId && !x.IsDeleted) // fix: was x.Id == roleId
                    .ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<RolePermission> CreateRolePermission(RolePermission model, int userId)
        {
            try
            {
                model.CreatedBy = userId;
                model.CreatedDate = DateTime.UtcNow;
                model.IsDeleted = false;

                await _db.RolePermissions.AddAsync(model);
                await _db.SaveChangesAsync();

                return model;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to create role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }

        public async Task<RolePermission> UpdateRolePermission(RolePermission model, int userId)
        {
            try
            {
                var existing = await _db.RolePermissions.FindAsync(model.Id)
                    ?? throw new KeyNotFoundException($"RolePermission with Id {model.Id} was not found.");

                existing.RoleId = model.RoleId;
                existing.PermissionId = model.PermissionId;
                existing.ModifiedBy = userId;
                existing.ModifiedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                return existing;
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
                var existing = await _db.RolePermissions.FindAsync(id)
                    ?? throw new KeyNotFoundException($"RolePermission with Id {id} was not found.");

                existing.IsDeleted = true;
                existing.ModifiedBy = userId;
                existing.ModifiedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to delete role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw;
            }
        }
    }
}
