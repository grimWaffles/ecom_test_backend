using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
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
    }
    public class ProductGrpcClient : IProductGrpcClient
    {
        private readonly ProductService.ProductServiceClient _client;
        private readonly MicroServiceUrl _urls;

        public ProductGrpcClient(IOptions<MicroServiceUrl> microserviceUrls)
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

            _client = new ProductService.ProductServiceClient(channel);
        }

        public async Task<ProductResponse> CreateProductAsync(int userId, ProductDto dto)
        {
            var request = new ProductRequest { UserId = userId, Dto = dto };
            return await _client.CreateProductAsync(request);
        }

        public async Task<ProductDto> GetProductByIdAsync(int id)
        {
            var request = new ProductIdRequest { Id = id };
            return await _client.GetProductByIdAsync(request);
        }

        public async Task<List<ProductDto>> GetAllProductsAsync()
        {
            var response = await _client.GetAllProductsAsync(new Empty());
            return response.Dtos.ToList();
        }

        public async Task<ProductResponse> UpdateProductAsync(int userId, ProductDto dto)
        {
            var request = new ProductRequest { UserId = userId, Dto = dto };
            return await _client.UpdateProductAsync(request);
        }

        public async Task<ProductResponse> DeleteProductAsync(int id, int modifiedBy)
        {
            var request = new ProductDeleteRequest { Id = id, ModifiedBy = modifiedBy };
            return await _client.DeleteProductAsync(request);
        }
    }
}
