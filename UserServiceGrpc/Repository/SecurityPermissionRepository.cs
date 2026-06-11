using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository
{
    public interface ISecurityPermissionRepository
    {
        Task<List<SecurityPermission>> GetAllAsync();
        Task<SecurityPermission?> GetByIdAsync(int id);
        Task<SecurityPermission?> CreateAsync(SecurityPermission permission);
        Task<SecurityPermission?> UpdateAsync(SecurityPermission permission);
        Task<bool> DeleteAsync(int id, int modifiedBy);
    }

    public class SecurityPermissionRepository : ISecurityPermissionRepository
    {
        private readonly AppDbContext _context;

        public SecurityPermissionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<SecurityPermission>> GetAllAsync()
        {
            try
            {
                return await _context.SecurityPermissions
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();
            }
            catch
            {
                return new List<SecurityPermission>();
            }
        }

        public async Task<SecurityPermission?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.SecurityPermissions
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            }
            catch
            {
                return null;
            }
        }

        public async Task<SecurityPermission?> CreateAsync(SecurityPermission permission)
        {
            try
            {
                await _context.SecurityPermissions.AddAsync(permission);
                await _context.SaveChangesAsync();

                return permission;
            }
            catch
            {
                return null;
            }
        }

        public async Task<SecurityPermission?> UpdateAsync(SecurityPermission permission)
        {
            try
            {
                _context.SecurityPermissions.Update(permission);
                await _context.SaveChangesAsync();

                return permission;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> DeleteAsync(int id, int modifiedBy)
        {
            try
            {
                SecurityPermission? permission = await _context.SecurityPermissions
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (permission == null)
                    return false;

                permission.IsDeleted = true;
                permission.ModifiedBy = modifiedBy;
                permission.ModifiedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
