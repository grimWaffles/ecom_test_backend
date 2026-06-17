using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface ISecurityPermissionService
    {
        Task<List<SecurityPermission>> GetAllAsync();
        Task<SecurityPermission?> GetByIdAsync(int id);
        Task<SecurityPermission?> CreateAsync(SecurityPermission permission);
        Task<SecurityPermission?> UpdateAsync(SecurityPermission permission);
        Task<bool> DeleteAsync(int id, int modifiedBy);
    }

    public class SecurityPermissionService : ISecurityPermissionService
    {
        private readonly ISecurityPermissionRepository _permissionRepository;

        public SecurityPermissionService(
            ISecurityPermissionRepository permissionRepository)
        {
            _permissionRepository = permissionRepository;
        }

        public async Task<List<SecurityPermission>> GetAllAsync()
        {
            try
            {
                return await _permissionRepository.GetAllAsync();
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
                return await _permissionRepository.GetByIdAsync(id);
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
                return await _permissionRepository.CreateAsync(permission);
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
                return await _permissionRepository.UpdateAsync(permission);
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
                return await _permissionRepository.DeleteAsync(id, modifiedBy);
            }
            catch
            {
                return false;
            }
        }
    }
}
