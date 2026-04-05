using API_Gateway.Grpc;
using API_Gateway.Services.API_Gateway.Services;
using ApiGateway.Protos;
using Grpc.Core;

namespace API_Gateway.Services
{
    namespace API_Gateway.Services
    {
        public interface IUserService
        {
            // User APIs
            Task<string> TestServiceAsync();

            Task<CreateUserRequest> GetUserByIdAsync(int userId);
            Task<List<CreateUserRequest>> GetAllUsersAsync();
            Task<List<CreateUserRequest>> GetAllUsersStreamAsync();

            Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user);
            Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user);
            Task<UserCrudResponse> DeleteUserAsync(int id, int userId);

            Task<UserLoginResponse> LoginUserAsync(string username, string password);
            Task<UserLoginResponse> LogoutUserAsync(int userId);

            // Role Permissions APIs
            Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id);
            Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync();
            Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId);
            Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(int roleId, string apiPath);
            Task<RolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto);
            Task<RolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto);
            Task<bool> DeleteRolePermissionAsync(int id);
        }
    }

    public class UserService : IUserService
    {
        private readonly IUserGrpcClient _grpc;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserGrpcClient grpc, ILogger<UserService> logger)
        {
            _grpc = grpc;
            _logger = logger;
        }

        public Task<string> TestServiceAsync()
            => _grpc.TestServiceAsync();

        public Task<List<CreateUserRequest>> GetAllUsersAsync()
            => _grpc.GetAllUsersAsync();

        public Task<List<CreateUserRequest>> GetAllUsersStreamAsync()
            => _grpc.GetAllUsersStreamAsync();

        public Task<CreateUserRequest> GetUserByIdAsync(int userId)
            => _grpc.GetUserByIdAsync(userId);

        public Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user)
            => _grpc.CreateUserAsync(user);

        public Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user)
            => _grpc.UpdateUserAsync(user);

        public Task<UserCrudResponse> DeleteUserAsync(int id, int userId)
            => _grpc.DeleteUserAsync(new UserRequestSingle { Id = id, UserId = userId });

        public Task<UserLoginResponse> LoginUserAsync(string username, string password)
            => _grpc.LoginUserAsync(new UserLoginRequest
            {
                Username = username,
                Password = password
            });

        public Task<UserLoginResponse> LogoutUserAsync(int userId)
            => _grpc.LogoutUserAsync(new UserRequestSingle { UserId = userId });

        // ROLE PERMISSIONS

        public async Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id)
        {
            try
            {
                return await _grpc.GetRolePermissionByIdAsync(new GetRolePermissionByIdRequest { Id = id });
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Not found: {Id}", id);
                return null;
            }
        }

        public async Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync()
        {
            var res = await _grpc.GetAllRolePermissionsAsync();
            return res.Items;
        }

        public async Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId)
        {
            var res = await _grpc.GetRolePermissionsByRoleIdAsync(
                new GetRolePermissionsByRoleIdRequest { RoleId = roleId });

            return res.Items;
        }

        public async Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(int roleId, string apiPath)
        {
            try
            {
                return await _grpc.GetRolePermissionByRoleIdAndPathAsync(
                    new GetRolePermissionByRoleIdAndPathRequest
                    {
                        RoleId = roleId,
                        ApiPath = apiPath
                    });
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<RolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto)
        {
            try
            {
                return await _grpc.CreateRolePermissionAsync(dto);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
                return null;
            }
        }

        public async Task<RolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto)
        {
            try
            {
                return await _grpc.UpdateRolePermissionAsync(dto);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<bool> DeleteRolePermissionAsync(int id)
        {
            try
            {
                var res = await _grpc.DeleteRolePermissionAsync(
                    new DeleteRolePermissionRequest { Id = id });

                return res.Success;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }
    }
}