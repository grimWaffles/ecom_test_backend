using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository
{
    public interface IRoleRepository
    {
        Task<List<Role>> GetAllAsync();
        Task<Role?> GetByIdAsync(int id);
        Task<Role?> CreateAsync(Role role);
        Task<Role?> UpdateAsync(Role role);
        Task<bool> DeleteAsync(int id, int modifiedBy);
    }
    public class RoleRepository : IRoleRepository
    {
        private readonly AppDbContext _context;

        public RoleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Role>> GetAllAsync()
        {
            try
            {
                return await _context.Roles
                    .Where(x => !x.IsDeleted)
                    .ToListAsync();
            }
            catch
            {
                return new List<Role>();
            }
        }

        public async Task<Role?> GetByIdAsync(int id)
        {
            try
            {
                return await _context.Roles
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            }
            catch
            {
                return null;
            }
        }

        public async Task<Role?> CreateAsync(Role role)
        {
            try
            {
                await _context.Roles.AddAsync(role);
                await _context.SaveChangesAsync();

                return role;
            }
            catch
            {
                return null;
            }
        }

        public async Task<Role?> UpdateAsync(Role role)
        {
            try
            {
                _context.Roles.Update(role);
                await _context.SaveChangesAsync();

                return role;
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
                Role? role = await _context.Roles
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (role == null)
                    return false;

                role.IsDeleted = true;
                role.ModifiedBy = modifiedBy;
                role.ModifiedDate = DateTime.UtcNow;

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
