using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository
{
    public interface IRolePermissionRepository
    {
        Task<RolePermission> GetPermissionByRoleIdAsync(int roleId);
        Task<RolePermission> GetPermissionByIdAsync(int id);
        Task<RolePermission> GetPermissionByNameAsync(string permissionName);
        Task<RolePermission> CreateAsync(RolePermission rolePermission, int userId);
        Task<RolePermission> UpdateAsync(RolePermission rolePermission, int userId);
        Task<bool> SoftDeleteAsync(int id, int userId);
    }

    public class RolePermissionRepository : IRolePermissionRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RolePermissionRepository> _logger;

        public RolePermissionRepository(AppDbContext context, ILogger<RolePermissionRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<RolePermission> GetPermissionByRoleIdAsync(int roleId)
        {
            try
            {
                return await _context.RolePermissions
                    .Include(rp => rp.Role)
                    .Include(rp => rp.Permission)
                    .FirstOrDefaultAsync(rp => rp.RoleId == roleId && !rp.IsDeleted);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while fetching permission for RoleId: {RoleId}", roleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching permission for RoleId: {RoleId}", roleId);
                throw;
            }
        }

        public async Task<RolePermission> GetPermissionByIdAsync(int id)
        {
            try
            {
                return await _context.RolePermissions
                    .Include(rp => rp.Role)
                    .Include(rp => rp.Permission)
                    .FirstOrDefaultAsync(rp => rp.Id == id && !rp.IsDeleted);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while fetching permission with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching permission with Id: {Id}", id);
                throw;
            }
        }

        public async Task<RolePermission> GetPermissionByNameAsync(string permissionName)
        {
            try
            {
                return await _context.RolePermissions
                    .Include(rp => rp.Role)
                    .Include(rp => rp.Permission)
                    .FirstOrDefaultAsync(rp => rp.Permission.Permission == permissionName && !rp.IsDeleted);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while fetching permission with Name: {PermissionName}", permissionName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while fetching permission with Name: {PermissionName}", permissionName);
                throw;
            }
        }

        public async Task<RolePermission> CreateAsync(RolePermission rolePermission, int userId)
        {
            try
            {
                rolePermission.CreatedBy = userId;
                rolePermission.CreatedDate = DateTime.UtcNow;

                await _context.RolePermissions.AddAsync(rolePermission);
                await _context.SaveChangesAsync();
                return rolePermission;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while creating RolePermission for RoleId: {RoleId}", rolePermission.RoleId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while creating RolePermission for RoleId: {RoleId}", rolePermission.RoleId);
                throw;
            }
        }

        public async Task<RolePermission> UpdateAsync(RolePermission rolePermission, int userId)
        {
            try
            {
                rolePermission.ModifiedBy = userId;
                rolePermission.ModifiedDate = DateTime.UtcNow;

                _context.RolePermissions.Update(rolePermission);
                await _context.SaveChangesAsync();
                return rolePermission;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while updating RolePermission with Id: {Id}", rolePermission.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while updating RolePermission with Id: {Id}", rolePermission.Id);
                throw;
            }
        }

        public async Task<bool> SoftDeleteAsync(int id, int userId)
        {
            try
            {
                var rolePermission = await _context.RolePermissions.FindAsync(id);
                if (rolePermission == null) return false;

                rolePermission.IsDeleted = true;
                rolePermission.ModifiedBy = userId;
                rolePermission.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "SQL error occurred while soft deleting RolePermission with Id: {Id}", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while soft deleting RolePermission with Id: {Id}", id);
                throw;
            }
        }
    }
}
