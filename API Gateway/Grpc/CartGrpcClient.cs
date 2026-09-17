using API_Gateway.Models;
using ApiGateway.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace API_Gateway.Grpc
{
    public interface ICartGrpcClient
    {
        Task<CartResponse> AddToCartAsync(AddToCartRequest request);
        Task<CartResponse> UpdateCartItemAsync(UpdateCartItemRequest request);
        Task<RemoveFromCartResponse> RemoveFromCartAsync(RemoveFromCartRequest request);
        Task<CartListResponse> ViewCartAsync(ViewCartRequest request);
    }
    public class CartGrpcClient : ICartGrpcClient
    {
        private readonly CartService.CartServiceClient _client;

        public CartGrpcClient(CartService.CartServiceClient client)
        {
            _client = client;
        }

        public async Task<CartResponse> AddToCartAsync(AddToCartRequest request)
        {
            return await _client.AddToCartAsync(request);
        }

        public async Task<CartResponse> UpdateCartItemAsync(UpdateCartItemRequest request)
        {
            return await _client.UpdateCartItemAsync(request);
        }

        public async Task<RemoveFromCartResponse> RemoveFromCartAsync(RemoveFromCartRequest request)
        {
            return await _client.RemoveFromCartAsync(request);
        }

        public async Task<CartListResponse> ViewCartAsync(ViewCartRequest request)
        {
            return await _client.ViewCartAsync(request);
        }
    }
}
