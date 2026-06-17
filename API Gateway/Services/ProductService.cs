using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API_Gateway.Services
{
    public interface IProductGrpcClient
    {
        Task<ProductResponse> CreateProductAsync(int userId, ProductDto dto);
        Task<ProductDto> GetProductByIdAsync(int id);
        Task<List<ProductDto>> GetAllProductsAsync();
        Task<ProductResponse> UpdateProductAsync(int userId, ProductDto dto);
        Task<ProductResponse> DeleteProductAsync(int id, int modifiedBy);
        Task<ProductServiceTestMessage> TestProductServiceHealth();
    }
    public class ProductGrpcClient : IProductGrpcClient
    {
        private readonly ProductService.ProductServiceClient _client;
        private readonly ILogger<ProductGrpcClient> _logger;
        private readonly DateTime rpcCallLimit = DateTime.UtcNow.AddSeconds(2);

        public ProductGrpcClient(ProductService.ProductServiceClient grpcClient, ILogger<ProductGrpcClient> logger)
        {
            _logger = logger;
            _client = grpcClient;
        }

        public async Task<ProductResponse> CreateProductAsync(int userId, ProductDto dto)
        {
            try
            {
                var request = new ProductRequest { UserId = userId, Dto = dto };
                return await _client.CreateProductAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while creating product for userId: {UserId}", userId);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while creating product for userId: {UserId}", userId);
                return null;
            }
        }

        public async Task<ProductDto> GetProductByIdAsync(int id)
        {
            try
            {
                var request = new ProductIdRequest { Id = id };
                return await _client.GetProductByIdAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting product with id: {ProductId}", id);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting product with id: {ProductId}", id);
                return null;
            }
        }

        public async Task<List<ProductDto>> GetAllProductsAsync()
        {
            try
            {
                var response = await _client.GetAllProductsAsync(new Empty());
                return response.Dtos.ToList();
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting all products");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting all products");
                return null;
            }
        }

        public async Task<ProductResponse> UpdateProductAsync(int userId, ProductDto dto)
        {
            try
            {
                var request = new ProductRequest { UserId = userId, Dto = dto };
                return await _client.UpdateProductAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while updating product for userId: {UserId}", userId);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while updating product for userId: {UserId}", userId);
                return null;
            }
        }

        public async Task<ProductResponse> DeleteProductAsync(int id, int modifiedBy)
        {
            try
            {
                var request = new ProductDeleteRequest { Id = id, ModifiedBy = modifiedBy };
                return await _client.DeleteProductAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while deleting product with id: {ProductId}", id);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while deleting product with id: {ProductId}", id);
                return null;
            }
        }

        public async Task<ProductServiceTestMessage> TestProductServiceHealth() 
        {
            try
            {
                return await _client.TestProductServiceAsync(new Empty(), deadline: rpcCallLimit);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while testing product service health");
                return new ProductServiceTestMessage() { StatusMessage = "Product service is down. "+ e.Message };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while testing product service health");
                return new ProductServiceTestMessage() { StatusMessage = "Product service is down" };
            }
        }
    }
}
