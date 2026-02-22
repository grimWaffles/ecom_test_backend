using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public class CustomerTransactionGrpcService : CustomerTransactionService.CustomerTransactionServiceBase
    {
        private readonly ICustomerTransactionRepository _repo;

        public CustomerTransactionGrpcService(ICustomerTransactionRepository repo)
        {
            _repo = repo;
        }

        public override async Task<TransactionResponseSingle> GetTransactionById(TransactionRequestSingle request, ServerCallContext context)
        {
            var model = await _repo.GetTransactionById(request.Id);
            if (model == null)
            {
                return new TransactionResponseSingle
                {
                    Status = 1,
                    ErrorMessage = "Transaction not found"
                };
            }

            return new TransactionResponseSingle
            {
                Status = 0,
                Dto = TransactionMapper.ToDto(model)
            };
        }

        public override async Task<TransactionResponseMultiple> GetAllTransactions(TransactionRequestMultiple request, ServerCallContext context)
        {
            var response = await _repo.GetAllTransactionsWithPagination(request);

            return response;
        }

        public override async Task<TransactionCrudResponse> AddTransaction(TransactionDto request, ServerCallContext context)
        {
            var result = await _repo.AddTransaction(request, (int)request.CreatedBy);

            return new TransactionCrudResponse
            {
                Status = result ? 0 : 1,
                ErrorMessage = result ? "" : "Failed to add transaction"
            };
        }

        public override async Task<TransactionCrudResponse> UpdateTransaction(TransactionDto request, ServerCallContext context)
        {
            var result = await _repo.UpdateTransaction(request, (int)request.ModifiedBy);

            return new TransactionCrudResponse
            {
                Status = result ? 0 : 1,
                ErrorMessage = result ? "" : "Failed to update transaction"
            };
        }

        public override async Task<TransactionCrudResponse> DeleteTransaction(TransactionRequestSingle request, ServerCallContext context)
        {
            // We’ll create a DTO from just the ID
            var dto = new TransactionDto
            {
                Id = request.Id
            };

            var result = await _repo.DeleteTransaction(dto, request.UserId);

            return new TransactionCrudResponse
            {
                Status = result ? 0 : 1,
                ErrorMessage = result ? "" : "Failed to delete transaction"
            };
        }
    }
}
