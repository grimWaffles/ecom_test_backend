using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository
{
    public interface IUserRepository
    {
        Task<List<UserModel>> GetUsers();
        Task<UserModel> GetUserById(int id);
        Task<int> CreateUser(UserModel user);
        Task<int> UpdateUser(UserModel user);
        Task<int> DeleteUser(UserModel user);
        Task<UserModel> GetUserByUsername(string username);

        Task<List<RoleAccess>> GetRolesAccessAsync();
    }

    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;

        public UserRepository(AppDbContext context)
        {
            _db = context;
        }

        public async Task<int> CreateUser(UserModel user)
        {
            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        public async Task<int> DeleteUser(UserModel user)
        {
            try
            {
                _db.Users.Update(user);
                await _db.SaveChangesAsync();

                return 1;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        public async Task<UserModel> GetUserById(int id)
        {
            try
            {
                UserModel u = await _db.Users.AsNoTracking()
                    .Include(u => u.Role)
                    .Where(u => u.Id == id)
                    .FirstAsync();

                return u;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<List<UserModel>> GetUsers()
        {
            try
            {
                List<UserModel> list = await _db.Users.AsNoTracking().Include(u => u.Role).Take(5).ToListAsync();

                return list;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public async Task<int> UpdateUser(UserModel user)
        {
            try
            {
                _db.Users.Update(user);

                await _db.SaveChangesAsync();

                return 1;
            }
            catch (Exception e)
            {
                return -1;
            }
        }

        public async Task<UserModel> GetUserByUsername(string username)
        {
            try
            {
                UserModel user = await _db.Users.AsNoTracking().Include(u=>u.Role).Where(u => u.Username == username)
                    .Select(u=> new UserModel
                {
                    Id = u.Id, Username = u.Username, Password = u.Password, RoleId = u.RoleId, Role = u.Role
                }).
                FirstAsync();

                return user;
            }
            catch(Exception e)
            {
                return null;
            }
        }

        public async Task<List<RoleAccess>> GetRolesAccessAsync()
        {
            try
            {
                return await _db.RoleAccesses.AsNoTracking().ToListAsync();
            }
            catch(Exception e)
            {
                return null;
            }
        }
    }
}
