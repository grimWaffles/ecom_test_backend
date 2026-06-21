using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SellerGrpcClient> _logger;

    public SellerGrpcClient(Seller.SellerClient grpcClient, ILogger<SellerGrpcClient> logger)
    {
        _logger = logger;
        _client = grpcClient;
    }

    public async Task<SellerResponse> CreateSellerAsync(int userId, SellerDto dto)
    {
        try
        {
            var request = new SellerRequest { UserId = userId, Dto = dto };
            return await _client.CreateSellerAsync(request);
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "RPC error occurred while creating seller for userId: {UserId}", userId);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while creating seller for userId: {UserId}", userId);
            return null;
        }
    }

    public async Task<SellerDto> GetSellerByIdAsync(int id)
    {
        try
        {
            return await _client.GetSellerByIdAsync(new SellerSingleRequest { Id = id });
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "RPC error occurred while getting seller with id: {SellerId}", id);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while getting seller with id: {SellerId}", id);
            return null;
        }
    }

    public async Task<List<SellerDto>> GetAllSellersAsync()
    {
        try
        {
            var response = await _client.GetAllSellersAsync(new Empty());
            return response.Sellers.ToList();
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "RPC error occurred while getting all sellers");
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while getting all sellers");
            return null;
        }
    }

    public async Task<SellerResponse> UpdateSellerAsync(int userId, SellerDto dto)
    {
        try
        {
            var request = new SellerRequest { UserId = userId, Dto = dto };
            return await _client.UpdateSellerAsync(request);
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "RPC error occurred while updating seller for userId: {UserId}", userId);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while updating seller for userId: {UserId}", userId);
            return null;
        }
    }

    public async Task<SellerResponse> DeleteSellerAsync(int id, int userId)
    {
        try
        {
            return await _client.DeleteSellerAsync(new SellerDeleteRequest { Id = id, UserId = userId });
        }
        catch (RpcException e)
        {
            _logger.LogError(e, "RPC error occurred while deleting seller with id: {SellerId}", id);
            return null;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while deleting seller with id: {SellerId}", id);
            return null;
        }
    }
}
