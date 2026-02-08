using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
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

        public ProductCategoryGrpcClient(IOptions<MicroServiceUrl> microserviceUrls)
        {
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
            var request = new ProductCategoryCreateRequest
            {
                UserId = userId,
                Dto = dto
            };
            return await _client.CreateCategoryAsync(request);
        }

        public async Task<ProductCategoryDto> GetCategoryByIdAsync(int id)
        {
            var request = new ProductCategorySingleRequest { Id = id };
            return await _client.GetCategoryByIdAsync(request);
        }

        public async Task<List<ProductCategoryDto>> GetAllCategoriesAsync()
        {
            var response = await _client.GetAllCategoriesAsync(new Empty());
            return response.Dtos.ToList();
        }

        public async Task<ProductCategoryCreateResponse> UpdateCategoryAsync(int userId, ProductCategoryDto dto)
        {
            var request = new ProductCategoryCreateRequest
            {
                UserId = userId,
                Dto = dto
            };
            return await _client.UpdateCategoryAsync(request);
        }

        public async Task<ProductCategoryCreateResponse> DeleteCategoryAsync(int id, int userId)
        {
            var request = new ProductCategoryDeleteRequest
            {
                Id = id,
                Userid = userId
            };
            return await _client.DeleteCategoryAsync(request);
        }
    }
}
