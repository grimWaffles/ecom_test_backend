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
        Task<string> TestServiceAsync();
        Task<CreateUserRequest> GetUserByIdAsync(int userId);
        Task<List<CreateUserRequest>> GetAllUsersAsync();
        Task<List<CreateUserRequest>> GetAllUsersStreamAsync();
        Task<UserCrudResponse> CreateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> UpdateUserAsync(CreateUserRequest user);
        Task<UserCrudResponse> DeleteUserAsync(int id,int userId);
        Task<UserLoginResponse> LoginUserAsync(string username, string password);
        Task<UserLoginResponse> LogoutUserAsync(int userId);
    }
    public class UserServiceClient : IUserServiceClient
    {
        private readonly User.UserClient _client;
        private readonly MicroServiceUrl _urls;

        public UserServiceClient(IOptions<MicroServiceUrl> microserviceUrls)
        {
            _urls = microserviceUrls.Value;

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
    }
}
