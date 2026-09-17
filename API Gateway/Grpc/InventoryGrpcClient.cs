using API_Gateway.Models;
using ApiGateway.Protos;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace API_Gateway.Grpc
{
    public interface IInventoryGrpcClient
    {
        Task<InventoryListResponse> GetAllInventoryAsync(GetAllInventoryRequest request);
        Task<InventoryListResponse> GetInventoryByProductIdAsync(GetInventoryByProductIdRequest request);
        Task<InventoryResponse> CreateInventoryAsync(CreateInventoryRequest request);
        Task<InventoryResponse> UpdateInventoryAsync(UpdateInventoryRequest request);
        Task<DeleteInventoryResponse> DeleteInventoryAsync(DeleteInventoryRequest request);
        Task<InventoryListResponse> GetInventoryByProductCategoryAsync(GetInventoryByProductCategoryRequest request);
    }
    public class InventoryGrpcClient : IInventoryGrpcClient
    {
        private readonly InventoryService.InventoryServiceClient _client;

        public InventoryGrpcClient(InventoryService.InventoryServiceClient client)
        {
            _client = client;
        }

        public async Task<InventoryListResponse> GetAllInventoryAsync(GetAllInventoryRequest request)
        {
            return await _client.GetAllInventoryAsync(request);
        }

        public async Task<InventoryListResponse> GetInventoryByProductIdAsync(GetInventoryByProductIdRequest request)
        {
            return await _client.GetInventoryByProductIdAsync(request);
        }

        public async Task<InventoryResponse> CreateInventoryAsync(CreateInventoryRequest request)
        {
            return await _client.CreateInventoryAsync(request);
        }

        public async Task<InventoryResponse> UpdateInventoryAsync(UpdateInventoryRequest request)
        {
            return await _client.UpdateInventoryAsync(request);
        }

        public async Task<DeleteInventoryResponse> DeleteInventoryAsync(DeleteInventoryRequest request)
        {
            return await _client.DeleteInventoryAsync(request);
        }

        public async Task<InventoryListResponse> GetInventoryByProductCategoryAsync(GetInventoryByProductCategoryRequest request)
        {
            return await _client.GetInventoryByProductCategoryAsync(request);
        }
    }
}
