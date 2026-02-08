using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

public interface ISellerGrpcClient
{
    Task<SellerResponse> CreateSellerAsync(int userId, SellerDto dto);
    Task<SellerDto> GetSellerByIdAsync(int id);
    Task<List<SellerDto>> GetAllSellersAsync();
    Task<SellerResponse> UpdateSellerAsync(int userId, SellerDto dto);
    Task<SellerResponse> DeleteSellerAsync(int id, int userId);
}

public class SellerGrpcClient : ISellerGrpcClient
{
    private readonly Seller.SellerClient _client;
    private readonly MicroServiceUrl _urls;

    public SellerGrpcClient(IOptions<MicroServiceUrl> microserviceUrls)
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

        _client = new Seller.SellerClient(channel);
    }

    public async Task<SellerResponse> CreateSellerAsync(int userId, SellerDto dto)
    {
        var request = new SellerRequest { UserId = userId, Dto = dto };
        return await _client.CreateSellerAsync(request);
    }

    public async Task<SellerDto> GetSellerByIdAsync(int id)
    {
        return await _client.GetSellerByIdAsync(new SellerSingleRequest { Id = id });
    }

    public async Task<List<SellerDto>> GetAllSellersAsync()
    {
        var response = await _client.GetAllSellersAsync(new Empty());
        return response.Sellers.ToList();
    }

    public async Task<SellerResponse> UpdateSellerAsync(int userId, SellerDto dto)
    {
        var request = new SellerRequest { UserId = userId, Dto = dto };
        return await _client.UpdateSellerAsync(request);
    }

    public async Task<SellerResponse> DeleteSellerAsync(int id, int userId)
    {
        return await _client.DeleteSellerAsync(new SellerDeleteRequest { Id = id, UserId = userId });
    }
}
