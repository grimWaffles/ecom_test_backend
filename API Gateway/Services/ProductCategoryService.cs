using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API_Gateway.Services
{
    public interface IProductCategoryGrpcClient
    {
        Task<ProductCategoryCreateResponse> CreateCategoryAsync(int userId, ProductCategoryDto dto);
        Task<ProductCategoryDto> GetCategoryByIdAsync(int id);
        Task<List<ProductCategoryDto>> GetAllCategoriesAsync();
        Task<ProductCategoryCreateResponse> UpdateCategoryAsync(int userId, ProductCategoryDto dto);
        Task<ProductCategoryCreateResponse> DeleteCategoryAsync(int id, int userId);
    }

    public class ProductCategoryGrpcClient : IProductCategoryGrpcClient
    {
        private readonly ProductCategory.ProductCategoryClient _client;
        private readonly MicroServiceUrl _urls;
        private readonly ILogger<ProductCategoryGrpcClient> _logger;

        public ProductCategoryGrpcClient(IOptions<MicroServiceUrl> microserviceUrls, ILogger<ProductCategoryGrpcClient> logger)
        {
            _logger = logger;
            _urls = microserviceUrls.Value;

            if (string.IsNullOrEmpty(_urls.GetProductServiceUrl()))
            {
                throw new ArgumentException("gRPC service URL not configured in appsettings.json");
            }

            var httpHandler = new HttpClientHandler
            {
                // This is optional and should be used only in development for insecure certs
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var channel = GrpcChannel.ForAddress(_urls.GetProductServiceUrl(), new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            _client = new ProductCategory.ProductCategoryClient(channel);
        }

        public async Task<ProductCategoryCreateResponse> CreateCategoryAsync(int userId, ProductCategoryDto dto)
        {
            try
            {
                var request = new ProductCategoryCreateRequest
                {
                    UserId = userId,
                    Dto = dto
                };
                return await _client.CreateCategoryAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while creating product category for userId: {UserId}", userId);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while creating product category for userId: {UserId}", userId);
                return null;
            }
        }

        public async Task<ProductCategoryDto> GetCategoryByIdAsync(int id)
        {
            try
            {
                var request = new ProductCategorySingleRequest { Id = id };
                return await _client.GetCategoryByIdAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting product category with id: {CategoryId}", id);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting product category with id: {CategoryId}", id);
                return null;
            }
        }

        public async Task<List<ProductCategoryDto>> GetAllCategoriesAsync()
        {
            try
            {
                var response = await _client.GetAllCategoriesAsync(new Empty());
                return response.Dtos.ToList();
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while getting all product categories");
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while getting all product categories");
                return null;
            }
        }

        public async Task<ProductCategoryCreateResponse> UpdateCategoryAsync(int userId, ProductCategoryDto dto)
        {
            try
            {
                var request = new ProductCategoryCreateRequest
                {
                    UserId = userId,
                    Dto = dto
                };
                return await _client.UpdateCategoryAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while updating product category for userId: {UserId}", userId);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while updating product category for userId: {UserId}", userId);
                return null;
            }
        }

        public async Task<ProductCategoryCreateResponse> DeleteCategoryAsync(int id, int userId)
        {
            try
            {
                var request = new ProductCategoryDeleteRequest
                {
                    Id = id,
                    Userid = userId
                };
                return await _client.DeleteCategoryAsync(request);
            }
            catch (RpcException e)
            {
                _logger.LogError(e, "RPC error occurred while deleting product category with id: {CategoryId}", id);
                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while deleting product category with id: {CategoryId}", id);
                return null;
            }
        }
    }
}
