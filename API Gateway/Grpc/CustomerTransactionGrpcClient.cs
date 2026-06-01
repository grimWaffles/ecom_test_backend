using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace API_Gateway.Grpc
{
    public interface ICustomerTransactionGrpcClient
    {
        Task<TransactionResponseSingle> GetTransactionByIdAsync(TransactionRequestSingle request);
        Task<TransactionResponseMultiple> GetAllTransactionsAsync(TransactionRequestMultiple request);
        Task<TransactionCrudResponse> AddTransactionAsync(TransactionDto request);
        Task<TransactionCrudResponse> UpdateTransactionAsync(TransactionDto request);
        Task<TransactionCrudResponse> DeleteTransactionAsync(TransactionRequestSingle request);
        Task<TransactionCrudResponse> TestCustomerTransactionServiceAsync(Empty empty);
    }

    public class CustomerTransactionGrpcClient : ICustomerTransactionGrpcClient
    {
        private readonly CustomerTransactionService.CustomerTransactionServiceClient _client;
        private readonly MicroServiceUrl _urls;

        public CustomerTransactionGrpcClient(IOptions<MicroServiceUrl> microserviceUrls)
        {
            _urls = microserviceUrls.Value;

            if (string.IsNullOrEmpty(_urls.GetOrderServiceUrl()))
                throw new ArgumentException("Transaction gRPC service URL not configured in appsettings.json");

            var httpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var channel = GrpcChannel.ForAddress(_urls.GetOrderServiceUrl(), new GrpcChannelOptions
            {
                HttpHandler = httpHandler
            });

            _client = new CustomerTransactionService.CustomerTransactionServiceClient(channel);
        }

        public async Task<TransactionResponseSingle> GetTransactionByIdAsync(TransactionRequestSingle request)
            => await _client.GetTransactionByIdAsync(request);

        public async Task<TransactionResponseMultiple> GetAllTransactionsAsync(TransactionRequestMultiple request)
            => await _client.GetAllTransactionsAsync(request);

        public async Task<TransactionCrudResponse> AddTransactionAsync(TransactionDto request)
            => await _client.AddTransactionAsync(request);

        public async Task<TransactionCrudResponse> UpdateTransactionAsync(TransactionDto request)
            => await _client.UpdateTransactionAsync(request);

        public async Task<TransactionCrudResponse> DeleteTransactionAsync(TransactionRequestSingle request)
            => await _client.DeleteTransactionAsync(request);

        public async Task<TransactionCrudResponse> TestCustomerTransactionServiceAsync(Empty empty)
            => await _client.TestCustomerTransactionGrpcServiceAsync(empty);
    }
}
