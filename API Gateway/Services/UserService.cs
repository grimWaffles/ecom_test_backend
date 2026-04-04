using Grpc.Net.Client;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Identity;
using API_Gateway.Models;
using Microsoft.Extensions.Options;
namespace API_Gateway.Services
{
    public interface IUserServiceClient
    {
        //User APIs
        Task<string> TestServiceAsync();
        Task<CreateUserRequest> GetUserByIdAsync(int userId);
        Task<List<CreateUserRequest>> GetAllUsersAsync();
        Task<List<CreateUserRequest>> GetAllUsersStreamAsync();
        Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> DeleteUserAsync(int id, int userId);
        Task<UserLoginResponse> LoginUserAsync(string username, string password);
        Task<UserLoginResponse> LogoutUserAsync(int userId);

        //RolePermissions APIs
        Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id);
        Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync();
        Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId);
        Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(int roleId, string apiPath);
        Task<RolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto);
        Task<RolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto);
        Task<bool> DeleteRolePermissionAsync(int id);
    }
    public class UserServiceClient : IUserServiceClient
    {
        private readonly User.UserClient _client;
        private readonly MicroServiceUrl _urls;
        private readonly ILogger<UserServiceClient> _logger;

        public UserServiceClient(IOptions<MicroServiceUrl> microserviceUrls, ILogger<UserServiceClient> logger)
        {
            _urls = microserviceUrls.Value;
            _logger = logger;

            GrpcChannel channel = GrpcChannel.ForAddress(_urls.GetUserServiceUrl());
            _client = new User.UserClient(channel);
        }

        // Test connectivity
        public async Task<string> TestServiceAsync()
        {
            var response = await _client.TestServiceAsync(new Empty());
            return response.ServiceStatus;
        }

        // Get all users (streaming)
        public async Task<List<CreateUserRequest>> GetAllUsersStreamAsync()
        {
            var result = new List<CreateUserRequest>();
            using var call = _client.GetAllUsersStream(new Empty());

            while (await call.ResponseStream.MoveNext())
            {
                result.Add(call.ResponseStream.Current);
            }

            return result;
        }

        // Get all users (non-streaming)
        public async Task<List<CreateUserRequest>> GetAllUsersAsync()
        {
            var response = await _client.GetAllUsersAsync(new Empty());
            return new List<CreateUserRequest>(response.Users);
        }

        // Get user by ID
        public async Task<CreateUserRequest> GetUserByIdAsync(int userId)
        {
            var request = new UserRequestSingle { Id = userId };
            return await _client.GetUserByIdAsyncAsync(request);
        }

        // Create user
        public async Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user)
        {
            return await _client.CreateUserAsync(user);
        }

        // Update user
        public async Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user)
        {
            return await _client.UpdateUserAsync(user);
        }

        // Delete user
        public async Task<UserCrudResponse> DeleteUserAsync(int id, int userId)
        {
            var request = new UserRequestSingle { Id = id, UserId = userId };
            return await _client.DeleteUserAsync(request);
        }

        // Login
        public async Task<UserLoginResponse> LoginUserAsync(string username, string password)
        {
            var request = new UserLoginRequest
            {
                Username = username,
                Password = password
            };
            return await _client.LoginUserAsync(request);
        }

        // Logout
        public async Task<UserLoginResponse> LogoutUserAsync(int userId)
        {
            var request = new UserRequestSingle { UserId = userId };
            return await _client.LogoutUserAsync(request);
        }

        //User Role Permissions
        // ── GetRolePermissionById ────────────────────────────────────────────────────

        public async Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id)
        {
            try
            {
                var request = new GetRolePermissionByIdRequest { Id = id };
                return await _client.GetRolePermissionByIdAsync(request);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("RolePermission with Id {Id} was not found.", id);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error in GetRolePermissionByIdAsync for Id {Id}", id);
                throw;
            }
        }

        // ── GetAllRolePermissions ────────────────────────────────────────────────────

        public async Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync()
        {
            try
            {
                var request = new GetAllRolePermissionsRequest();
                var response = await _client.GetAllRolePermissionsAsync(request);
                return response.Items;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "gRPC error in GetAllRolePermissionsAsync");
                throw;
            }
        }

        // ── GetRolePermissionsByRoleId ───────────────────────────────────────────────

        public async Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId)
        {
            try
            {
                var request = new GetRolePermissionsByRoleIdRequest { RoleId = roleId };
                var response = await _client.GetRolePermissionsByRoleIdAsync(request);
                return response.Items;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogWarning("Invalid RoleId {RoleId} passed to GetRolePermissionsByRoleIdAsync.", roleId);
                throw;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "gRPC error in GetRolePermissionsByRoleIdAsync for RoleId {RoleId}", roleId);
                throw;
            }
        }

        // ── GetRolePermissionByRoleIdAndPath ─────────────────────────────────────────

        public async Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(
            int roleId, string apiPath)
        {
            try
            {
                var request = new GetRolePermissionByRoleIdAndPathRequest
                {
                    RoleId = roleId,
                    ApiPath = apiPath
                };
                return await _client.GetRolePermissionByRoleIdAndPathAsync(request);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning(
                    "RolePermission not found for RoleId {RoleId} and ApiPath {ApiPath}.",
                    roleId, apiPath);
                return null;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "gRPC error in GetRolePermissionByRoleIdAndPathAsync for RoleId {RoleId} ApiPath {ApiPath}",
                    roleId, apiPath);
                throw;
            }
        }

        // ── CreateRolePermission ─────────────────────────────────────────────────────

        public async Task<RolePermissionResponse?> CreateRolePermissionAsync(
            CreateRolePermissionRequest dto)
        {
            try
            {
                return await _client.CreateRolePermissionAsync(dto);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.AlreadyExists)
            {
                _logger.LogWarning(
                    "Duplicate RolePermission for RoleId {RoleId} and ApiPath {ApiPath}. Detail: {Detail}",
                    dto.RoleId, dto.ApiPath, ex.Status.Detail);
                return null;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogWarning(
                    "Invalid argument in CreateRolePermissionAsync. Detail: {Detail}",
                    ex.Status.Detail);
                throw;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "gRPC error in CreateRolePermissionAsync for RoleId {RoleId}", dto.RoleId);
                throw;
            }
        }

        // ── UpdateRolePermission ─────────────────────────────────────────────────────

        public async Task<RolePermissionResponse?> UpdateRolePermissionAsync(
            UpdateRolePermissionRequest dto)
        {
            try
            {
                return await _client.UpdateRolePermissionAsync(dto);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning(
                    "RolePermission with Id {Id} was not found for update.", dto.Id);
                return null;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogWarning(
                    "Invalid argument in UpdateRolePermissionAsync. Detail: {Detail}",
                    ex.Status.Detail);
                throw;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "gRPC error in UpdateRolePermissionAsync for Id {Id}", dto.Id);
                throw;
            }
        }

        // ── DeleteRolePermission ─────────────────────────────────────────────────────

        public async Task<bool> DeleteRolePermissionAsync(int id)
        {
            try
            {
                var request = new DeleteRolePermissionRequest { Id = id };
                var response = await _client.DeleteRolePermissionAsync(request);
                return response.Success;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("RolePermission with Id {Id} was not found for deletion.", id);
                return false;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogWarning(
                    "Invalid argument in DeleteRolePermissionAsync. Detail: {Detail}",
                    ex.Status.Detail);
                throw;
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "gRPC error in DeleteRolePermissionAsync for Id {Id}", id);
                throw;
            }
        }
    }
}
