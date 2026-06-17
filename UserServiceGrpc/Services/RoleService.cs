using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface IRoleService
    {
        Task<List<Role>> GetAllAsync();
        Task<Role?> GetByIdAsync(int id);
        Task<Role?> CreateAsync(Role role);
        Task<Role?> UpdateAsync(Role role);
        Task<bool> DeleteAsync(int id, int modifiedBy);
    }
    public class RoleService : IRoleService
    {
        private readonly IRoleRepository _roleRepository;

        public RoleService(IRoleRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<List<Role>> GetAllAsync()
        {
            try
            {
                return await _roleRepository.GetAllAsync();
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
                return await _roleRepository.GetByIdAsync(id);
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
                return await _roleRepository.CreateAsync(role);
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
                return await _roleRepository.UpdateAsync(role);
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
                return await _roleRepository.DeleteAsync(id, modifiedBy);
            }
            catch
            {
                return false;
            }
        }
    }
}
