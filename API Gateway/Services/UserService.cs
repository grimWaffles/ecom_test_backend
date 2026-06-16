using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using API_Gateway.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        Task<GetAllRolePermissionsByRoleIdResponse?> GetAllPermissionsByRoleId(int id);
        Task<CheckRoleIdAndPermissionResponse?> CheckRoleIdAndPermission(int id, string permissionName);
        Task<GetAllRolePermissionsByRoleIdResponse?> GetAllPermissionsByRoleIdAndPermissionName(int id, string permissionName);
        Task<CreateRolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto);
        Task<UpdateRolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto);
        Task<bool> DeleteRolePermissionAsync(int id);
    }

    public class UserService : IUserService, System.IDisposable
    {
        private readonly User.UserClient _client;
        private readonly GrpcChannel _channel;
        private readonly ILogger<UserService> _logger;

        public UserService(IOptions<MicroServiceUrl> options, ILogger<UserService> logger)
        {
            _channel = GrpcChannel.ForAddress(options.Value.GetUserServiceUrl());
            _client = new User.UserClient(_channel);
            _logger = logger;
        }

        public void Dispose()
        {
            _channel?.Dispose();
        }

        public async Task<string> TestServiceAsync()
        {
            try
            {
                var res = await _client.TestServiceAsync(new Empty());
                return res.ServiceStatus;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while testing user service");
                return "User service is down";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while testing user service");
                return "User service is down";
            }
        }

        public async Task<List<CreateUserRequest>> GetAllUsersAsync()
        {
            try
            {
                var res = await _client.GetAllUsersAsync(new Empty());
                return res.Users.ToList();
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while getting all users");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting all users");
                return null;
            }
        }

        public async Task<List<CreateUserRequest>> GetAllUsersStreamAsync()
        {
            try
            {
                var list = new List<CreateUserRequest>();
                using var call = _client.GetAllUsersStream(new Empty());

                while (await call.ResponseStream.MoveNext())
                {
                    list.Add(call.ResponseStream.Current);
                }

                return list;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while getting all users stream");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting all users stream");
                return null;
            }
        }

        public async Task<CreateUserRequest> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _client.GetUserByIdAsyncAsync(new UserRequestSingle() { UserId = userId }).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while getting user by id: {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting user by id: {UserId}", userId);
                return null;
            }
        }

        public async Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user)
        {
            try
            {
                return await _client.CreateUserAsync(user).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while creating user");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating user");
                return null;
            }
        }

        public async Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user)
        {
            try
            {
                return await _client.UpdateUserAsync(user).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while updating user");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating user");
                return null;
            }
        }

        public async Task<UserCrudResponse> DeleteUserAsync(int id, int userId)
        {
            try
            {
                return await _client.DeleteUserAsync(new UserRequestSingle { Id = id, UserId = userId }).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while deleting user with id: {Id}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting user with id: {Id}", id);
                return null;
            }
        }

        public async Task<UserLoginResponse> LoginUserAsync(string username, string password)
        {
            try
            {
                return await _client.LoginUserAsync(new UserLoginRequest
                {
                    Username = username,
                    Password = password
                }).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while logging in user: {Username}", username);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while logging in user: {Username}", username);
                return null;
            }
        }

        public async Task<UserLoginResponse> LogoutUserAsync(int userId)
        {
            try
            {
                return await _client.LogoutUserAsync(new UserRequestSingle { UserId = userId }).ResponseAsync;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while logging out user with id: {UserId}", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while logging out user with id: {UserId}", userId);
                return null;
            }
        }

        // ROLE PERMISSIONS
        public async Task<GetAllRolePermissionsByRoleIdResponse?> GetAllPermissionsByRoleId(int id)
        {
            try
            {
                return await _client.GetAllPermissionsByRoleIdAsync(new GetAllRolePermissionsByRoleIdRequest { RoleId = id }).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}", id);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while getting role permission by id: {Id}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting role permission by id: {Id}", id);
                return null;
            }
        }

        public async Task<GetAllRolePermissionsByRoleIdResponse?> GetAllPermissionsByRoleIdAndPermissionName(int id, string permissionName)
        {
            try
            {
                return await _client.GetAllPermissionsByRoleIdAndPermissionNameAsync(new GetAllRolePermissionsByRoleIdAndPermissionNameRequest { RoleId = id, PermissionName = permissionName }).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while getting role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
        }

        public async Task<CheckRoleIdAndPermissionResponse?> CheckRoleIdAndPermission(int id, string permissionName)
        {
            try
            {
                return await _client.CheckRoleIdAndPermissionAsync(new CheckRoleIdAndPermissionRequest { RoleId = id, PermissionName = permissionName }).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while checking role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking role permission by id, name: {Id}, {permission}", id, permissionName);
                return null;
            }
        }

        public async Task<CreateRolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto)
        {
            try
            {
                return await _client.CreateRolePermissionAsync(dto).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
                _logger.LogWarning("Role permission already exists");
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while creating role permission");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating role permission");
                return null;
            }
        }

        public async Task<UpdateRolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto)
        {
            try
            {
                return await _client.UpdateRolePermissionAsync(dto).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found for update");
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while updating role permission");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating role permission");
                return null;
            }
        }

        public async Task<bool> DeleteRolePermissionAsync(int id)
        {
            try
            {
                DeleteRolePermissionResponse res = await _client.DeleteRolePermissionAsync(new DeleteRolePermissionRequest { Id = id }).ResponseAsync;
                return res.Success;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Role permission not found for deletion: {Id}", id);
                return false;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RPC error occurred while deleting role permission: {Id}", id);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting role permission: {Id}", id);
                return false;
            }
        }
    }
}
