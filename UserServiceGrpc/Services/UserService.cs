using Microsoft.Extensions.Options;
using System.Security.Claims;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public interface IUserService
    {
        Task<List<UserModel>> GetUsers();
        Task<UserModel> GetUserById(int id);
        Task<UserModel> GetUserByUsername(string username);
        Task<List<RolePermission>> GetRolesAccessAsync();

        Task<ServiceResult> CreateUser(UserModel user);
        Task<ServiceResult> UpdateUser(UserModel user);
        Task<ServiceResult> DeleteUser(int userId, int deletedByUserId);

        Task<LoginResponseDto> LoginUser(string username, string password);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly JwtUserSchemaOptions _jwtUserSchema;
        private readonly ILogger<UserService> _logger;
        private readonly ITokenHelper _tokenHelper;

        public UserService(IUserRepository userRepository, ILogger<UserService> logger, IOptions<JwtUserSchemaOptions> schemaOptions, ITokenHelper tokenHelper)
        {
            _userRepository = userRepository;
            _logger = logger;
            _jwtUserSchema = schemaOptions.Value;
            _tokenHelper = tokenHelper;
        }

        public async Task<List<UserModel>> GetUsers()
        {
            try
            {
                _logger.LogInformation("Fetching all users.");
                var users = await _userRepository.GetUsers();
                _logger.LogInformation("Successfully retrieved {Count} user(s).", users?.Count ?? 0);
                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching users.");
                return null;
            }
        }

        public async Task<UserModel> GetUserById(int id)
        {
            try
            {
                _logger.LogInformation("Fetching user with ID {UserId}.", id);
                var user = await _userRepository.GetUserById(id);

                if (user == null)
                    _logger.LogWarning("No user found with ID {UserId}.", id);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching user with ID {UserId}.", id);
                return null;
            }
        }

        public async Task<UserModel> GetUserByUsername(string username)
        {
            try
            {
                _logger.LogInformation("Fetching user with username '{Username}'.", username);
                var user = await _userRepository.GetUserByUsername(username);

                if (user == null)
                    _logger.LogWarning("No user found with username '{Username}'.", username);

                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching user with username '{Username}'.", username);
                return null;
            }
        }

        public async Task<ServiceResult> CreateUser(UserModel user)
        {
            try
            {
                if (user == null)
                {
                    _logger.LogWarning("CreateUser called with a null user model.");
                    return ServiceResult.Error("User model cannot be null.");
                }

                var validationErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(user.Username))
                    validationErrors.Add("Username is required.");

                if (string.IsNullOrWhiteSpace(user.Password))
                    validationErrors.Add("Password is required.");

                if (user.RoleId <= 0)
                    validationErrors.Add("A valid RoleId is required.");

                if (validationErrors.Any())
                {
                    _logger.LogWarning("CreateUser validation failed: {Errors}", string.Join(" | ", validationErrors));
                    return ServiceResult.Failures(validationErrors);
                }

                UserModel existing = await _userRepository.GetUserByUsername(user.Username);
                var conflicts = new List<string>();

                if (existing != null)
                {
                    conflicts.Add("Username already exists.");

                    if (existing.Email == user.Email)
                        conflicts.Add("Email ID already exists.");

                    if (existing.MobileNo == user.MobileNo)
                        conflicts.Add("Mobile number already exists.");
                }

                if (conflicts.Any())
                {
                    _logger.LogWarning("CreateUser conflict(s) for username '{Username}': {Conflicts}",
                        user.Username, string.Join(" | ", conflicts));
                    return ServiceResult.Failures(conflicts);
                }

                _logger.LogInformation("Creating user with username '{Username}'.", user.Username);
                int rows = await _userRepository.CreateUser(user);

                if (rows == 1)
                {
                    _logger.LogInformation("User '{Username}' created successfully.", user.Username);
                    return ServiceResult.Success("User added successfully.");
                }

                _logger.LogError("Failed to create user '{Username}'.", user.Username);
                return ServiceResult.Failure("Failed to add user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating user '{Username}'.", user?.Username);
                return ServiceResult.Error("An unexpected error occurred while creating the user.");
            }
        }

        public async Task<ServiceResult> UpdateUser(UserModel user)
        {
            try
            {
                if (user == null)
                {
                    _logger.LogWarning("UpdateUser called with a null user model.");
                    return ServiceResult.Error("User model cannot be null.");
                }

                var validationErrors = new List<string>();

                if (user.Id <= 0)
                    validationErrors.Add("A valid Id is required.");

                if (string.IsNullOrWhiteSpace(user.Username))
                    validationErrors.Add("Username is required.");

                if (user.RoleId <= 0)
                    validationErrors.Add("A valid RoleId is required.");

                if (validationErrors.Any())
                {
                    _logger.LogWarning("UpdateUser validation failed: {Errors}", string.Join(" | ", validationErrors));
                    return ServiceResult.Failures(validationErrors);
                }

                UserModel existing = await _userRepository.GetUserById(user.Id);

                if (existing == null)
                {
                    _logger.LogWarning("UpdateUser: user with ID {UserId} does not exist.", user.Id);
                    return ServiceResult.Failure("User does not exist.");
                }

                existing.Username = user.Username;
                existing.Password = user.Password;
                existing.Email = user.Email;
                existing.MobileNo = user.MobileNo;
                existing.RoleId = user.RoleId;
                existing.ModifiedBy = user.ModifiedBy;
                existing.ModifiedDate = user.ModifiedDate;

                _logger.LogInformation("Updating user with ID {UserId}.", existing.Id);
                int rows = await _userRepository.UpdateUser(existing);

                if (rows == 1)
                {
                    _logger.LogInformation("User with ID {UserId} updated successfully.", existing.Id);
                    return ServiceResult.Success("User updated successfully.");
                }

                _logger.LogError("Failed to update user with ID {UserId}.", existing.Id);
                return ServiceResult.Failure("Failed to update user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating user with ID {UserId}.", user?.Id);
                return ServiceResult.Error("An unexpected error occurred while updating the user.");
            }
        }

        public async Task<ServiceResult> DeleteUser(int userId, int deletedByUserId)
        {
            try
            {
                if (userId <= 0)
                {
                    _logger.LogWarning("DeleteUser called with invalid ID {UserId}.", userId);
                    return ServiceResult.Error("A valid user ID is required.");
                }

                UserModel existing = await _userRepository.GetUserById(userId);

                if (existing == null)
                {
                    _logger.LogWarning("DeleteUser: user with ID {UserId} does not exist.", userId);
                    return ServiceResult.Failure("User does not exist.");
                }

                existing.IsDeleted = true;
                existing.ModifiedDate = DateTime.Now;
                existing.ModifiedBy = deletedByUserId;

                _logger.LogInformation("Soft-deleting user with ID {UserId}.", userId);
                int rows = await _userRepository.UpdateUser(existing);

                if (rows == 1)
                {
                    _logger.LogInformation("User with ID {UserId} soft-deleted successfully.", userId);
                    return ServiceResult.Success("User deleted successfully.");
                }

                _logger.LogError("Failed to soft-delete user with ID {UserId}.", userId);
                return ServiceResult.Failure("Failed to delete user.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting user with ID {UserId}.", userId);
                return ServiceResult.Error("An unexpected error occurred while deleting the user.");
            }
        }

        public async Task<List<RolePermission>> GetRolesAccessAsync()
        {
            try
            {
                _logger.LogInformation("Fetching role permissions.");
                var roles = await _userRepository.GetRolesAccessAsync();
                _logger.LogInformation("Successfully retrieved {Count} role permission(s).", roles?.Count ?? 0);
                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching role permissions.");
                return null;
            }
        }

        public async Task<LoginResponseDto> LoginUser(string username, string password)
        {
            UserModel model = await GetUserByUsername(username);
            LoginResponseDto loginResponseDto = new LoginResponseDto();

            if (model == null)
            {
                loginResponseDto.UserId = 0;
                loginResponseDto.ErrorMessage = "User does not exist";
                return loginResponseDto;
            }

            if (model.Password != password)
            {
                loginResponseDto.UserId = 0;
                loginResponseDto.ErrorMessage = "Password is incorrect";
                return loginResponseDto;
            }

            Claim[] claims =
            [
                new Claim( "UserId",   model.Id.ToString() ),
                new Claim( "RoleId",   model.RoleId.ToString() ),
                new Claim( "Username", model.Username ),
                new Claim( "Role",     model.Role.Name.ToString().ToUpper() )
            ];

            loginResponseDto.UserId = model.Id;
            loginResponseDto.Username = model.Username;
            loginResponseDto.AccessToken = _tokenHelper.GenerateJwtToken(claims);

            loginResponseDto.RoleId = model.RoleId;
            loginResponseDto.RoleName = model.Role.Name.ToString();
            loginResponseDto.ErrorMessage = "";

            return loginResponseDto;
        }
    }
}
