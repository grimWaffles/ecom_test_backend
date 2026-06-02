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
        Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id);
        Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync();
        Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId);
        Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(int roleId, string apiPath);
        Task<RolePermissionResponse?> CreateRolePermissionAsync(CreateRolePermissionRequest dto);
        Task<RolePermissionResponse?> UpdateRolePermissionAsync(UpdateRolePermissionRequest dto);
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
            var res = await _client.TestServiceAsync(new Empty());
            return res.ServiceStatus;
        }

        public async Task<List<CreateUserRequest>> GetAllUsersAsync()
        {
            var res = await _client.GetAllUsersAsync(new Empty());
            return res.Users.ToList();
        }

        public async Task<List<CreateUserRequest>> GetAllUsersStreamAsync()
        {
            var list = new List<CreateUserRequest>();
            using var call = _client.GetAllUsersStream(new Empty());

            while (await call.ResponseStream.MoveNext())
            {
                list.Add(call.ResponseStream.Current);
            }

            return list;
        }

        public Task<CreateUserRequest> GetUserByIdAsync(int userId)
            => _client.GetUserByIdAsyncAsync(new UserRequestSingle() { UserId = userId}).ResponseAsync;

        public Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user)
            => _client.CreateUserAsync(user).ResponseAsync;

        public Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user)
            => _client.UpdateUserAsync(user).ResponseAsync;

        public Task<UserCrudResponse> DeleteUserAsync(int id, int userId)
            => _client.DeleteUserAsync(new UserRequestSingle { Id = id, UserId = userId }).ResponseAsync;

        public Task<UserLoginResponse> LoginUserAsync(string username, string password)
            => _client.LoginUserAsync(new UserLoginRequest
            {
                Username = username,
                Password = password
            }).ResponseAsync;

        public Task<UserLoginResponse> LogoutUserAsync(int userId)
            => _client.LogoutUserAsync(new UserRequestSingle { UserId = userId }).ResponseAsync;

        // ROLE PERMISSIONS

        public async Task<RolePermissionResponse?> GetRolePermissionByIdAsync(int id)
        {
            try
            {
                return await _client.GetRolePermissionByIdAsync(new GetRolePermissionByIdRequest { Id = id }).ResponseAsync;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Not found: {Id}", id);
                return null;
            }
        }

        public async Task<IEnumerable<RolePermissionResponse>> GetAllRolePermissionsAsync()
        {
            var res = await _client.GetAllRolePermissionsAsync(new GetAllRolePermissionsRequest()).ResponseAsync;
            return res.Items;
        }

        public async Task<IEnumerable<RolePermissionResponse>> GetRolePermissionsByRoleIdAsync(int roleId)
        {
            var res = await _client.GetRolePermissionsByRoleIdAsync(new GetRolePermissionsByRoleIdRequest { RoleId = roleId }).ResponseAsync;
            return res.Items;
        }

        public async Task<RolePermissionResponse?> GetRolePermissionByRoleIdAndPathAsync(int roleId, string apiPath)
        {
            try
            {
                return await _client.GetRolePermissionByRoleIdAndPathAsync(new GetRolePermissionByRoleIdAndPathRequest
                {
                    RoleId = roleId,
                    ApiPath = apiPath
                }).ResponseAsync;
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
                return await _client.CreateRolePermissionAsync(dto).ResponseAsync;
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
                return await _client.UpdateRolePermissionAsync(dto).ResponseAsync;
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
                var res = await _client.DeleteRolePermissionAsync(new DeleteRolePermissionRequest { Id = id }).ResponseAsync;
                return res.Success;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }
    }
}
