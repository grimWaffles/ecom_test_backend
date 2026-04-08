using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace API_Gateway.Grpc
{
    public interface IUserGrpcClient
    {
        Task<string> TestServiceAsync();

        Task<List<CreateUserRequest>> GetAllUsersAsync();
        Task<List<CreateUserRequest>> GetAllUsersStreamAsync();
        Task<CreateUserRequest> GetUserByIdAsync(int userId);

        Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> DeleteUserAsync(UserRequestSingle request);

        Task<UserLoginResponse> LoginUserAsync(UserLoginRequest request);
        Task<UserLoginResponse> LogoutUserAsync(UserRequestSingle request);

        Task<RolePermissionResponse> GetRolePermissionByIdAsync(GetRolePermissionByIdRequest request);
        Task<GetAllRolePermissionsResponse> GetAllRolePermissionsAsync();
        Task<GetAllRolePermissionsResponse> GetRolePermissionsByRoleIdAsync(GetRolePermissionsByRoleIdRequest request);
        Task<RolePermissionResponse> GetRolePermissionByRoleIdAndPathAsync(GetRolePermissionByRoleIdAndPathRequest request);
        Task<RolePermissionResponse> CreateRolePermissionAsync(CreateRolePermissionRequest request);
        Task<RolePermissionResponse> UpdateRolePermissionAsync(UpdateRolePermissionRequest request);
        Task<DeleteRolePermissionResponse> DeleteRolePermissionAsync(DeleteRolePermissionRequest request);
    }

    public class UserGrpcClient : IUserGrpcClient
    {
        private readonly User.UserClient _client;

        public UserGrpcClient(IOptions<MicroServiceUrl> options)
        {
            var channel = GrpcChannel.ForAddress(options.Value.GetUserServiceUrl());
            _client = new User.UserClient(channel);
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

        public Task<UserCrudResponse> DeleteUserAsync(UserRequestSingle request)
            => _client.DeleteUserAsync(request).ResponseAsync;

        public Task<UserLoginResponse> LoginUserAsync(UserLoginRequest request)
            => _client.LoginUserAsync(request).ResponseAsync;

        public Task<UserLoginResponse> LogoutUserAsync(UserRequestSingle request)
            => _client.LogoutUserAsync(request).ResponseAsync;

        public Task<RolePermissionResponse> GetRolePermissionByIdAsync(GetRolePermissionByIdRequest request)
            => _client.GetRolePermissionByIdAsync(request).ResponseAsync;

        public Task<GetAllRolePermissionsResponse> GetAllRolePermissionsAsync()
            => _client.GetAllRolePermissionsAsync(new GetAllRolePermissionsRequest()).ResponseAsync;

        public Task<GetAllRolePermissionsResponse> GetRolePermissionsByRoleIdAsync(GetRolePermissionsByRoleIdRequest request)
            => _client.GetRolePermissionsByRoleIdAsync(request).ResponseAsync;

        public Task<RolePermissionResponse> GetRolePermissionByRoleIdAndPathAsync(GetRolePermissionByRoleIdAndPathRequest request)
            => _client.GetRolePermissionByRoleIdAndPathAsync(request).ResponseAsync;

        public Task<RolePermissionResponse> CreateRolePermissionAsync(CreateRolePermissionRequest request)
            => _client.CreateRolePermissionAsync(request).ResponseAsync;

        public Task<RolePermissionResponse> UpdateRolePermissionAsync(UpdateRolePermissionRequest request)
            => _client.UpdateRolePermissionAsync(request).ResponseAsync;

        public Task<DeleteRolePermissionResponse> DeleteRolePermissionAsync(DeleteRolePermissionRequest request)
            => _client.DeleteRolePermissionAsync(request).ResponseAsync;
    }
}