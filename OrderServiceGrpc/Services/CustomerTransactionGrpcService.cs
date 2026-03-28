using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Helpers.cs;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Repository;
using System.ComponentModel;

namespace OrderServiceGrpc.Services
{
    public class CustomerTransactionGrpcService : CustomerTransactionService.CustomerTransactionServiceBase
    {
        private readonly ICustomerTransactionProcessorService _transactionProcessorService;

        public CustomerTransactionGrpcService(ICustomerTransactionProcessorService transactionProcessorService)
        {
            _transactionProcessorService = transactionProcessorService;
        }

        public override async Task<TransactionResponseSingle> GetTransactionById(TransactionRequestSingle request, ServerCallContext context)
        {
            if (request.Id <= 0)
            {
                return new TransactionResponseSingle
                {
                    Status = 1,
                    ErrorMessage = "Invalid transaction ID"
                };
            }

            CustomerTransactionDto dto = await _transactionProcessorService.GetTransactionById(request.Id);

            if (dto == null)
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
                Dto = TransactionMapper.DtoToProto(dto)
            };
        }

        public override async Task<TransactionResponseMultiple> GetAllTransactions(TransactionRequestMultiple request, ServerCallContext context)
        {
            if (request.PageNumber <= 0)
            {
                return new TransactionResponseMultiple
                {
                    Status = false,
                    ErrorMessage = "Page number must be greater than 0"
                };
            }

            if (request.PageLength <= 0)
            {
                return new TransactionResponseMultiple
                {
                    Status = false,
                    ErrorMessage = "Page length must be greater than 0"
                };
            }

            if (request.StartDate != null && request.EndDate != null)
            {
                DateTime startDate = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
                DateTime endDate = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

                if (startDate > endDate)
                {
                    return new TransactionResponseMultiple
                    {
                        Status = false,
                        ErrorMessage = "Start date cannot be greater than end date"
                    };
                }
            }

            DateTime start = DateTimeHelper.ConvertTimestampToDateTime(request.StartDate);
            DateTime end = DateTimeHelper.ConvertTimestampToDateTime(request.EndDate);

            PagedTransactionResultFromService result = await _transactionProcessorService.GetAllTransactionsWithPagination(
                start, end, request.PageNumber, request.PageLength, request.TransactionType);

            return new TransactionResponseMultiple
            {
                Status = result.Status,
                ErrorMessage = result.ErrorMessage,
                TotalPages = result.TotalPages,
                TotalRows = result.TotalTransactions
            };
        }

        public override async Task<TransactionCrudResponse> AddTransaction(TransactionDto request, ServerCallContext context)
        {
            if (request.UserId <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Invalid user ID"
                };
            }

            if (string.IsNullOrWhiteSpace(request.TransactionType))
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Transaction type is required"
                };
            }

            if (request.Amount <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Amount must be greater than 0"
                };
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionKey) && request.TransactionKey.Length > 15)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Transaction key must not exceed 15 characters"
                };
            }

            int result = await _transactionProcessorService.AddTransaction(TransactionMapper.ProtoToDto(request), (int)request.CreatedBy);

            return new TransactionCrudResponse
            {
                Status = result <= 0 ? 0 : 1,
                ErrorMessage = result > 0 ? "" : "Failed to add transaction"
            };
        }

        public override async Task<TransactionCrudResponse> UpdateTransaction(TransactionDto request, ServerCallContext context)
        {
            if (request.Id <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Invalid transaction ID"
                };
            }

            if (request.UserId <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Invalid user ID"
                };
            }

            if (string.IsNullOrWhiteSpace(request.TransactionType))
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Transaction type is required"
                };
            }

            if (request.Amount <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Amount must be greater than 0"
                };
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionKey) && request.TransactionKey.Length > 15)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Transaction key must not exceed 15 characters"
                };
            }

            var result = await _transactionProcessorService.UpdateTransaction(TransactionMapper.ProtoToDto(request), (int)request.ModifiedBy);

            return new TransactionCrudResponse
            {
                Status = result ? 1 :0,
                ErrorMessage = result ? "" : "Failed to update transaction"
            };
        }

        public override async Task<TransactionCrudResponse> DeleteTransaction(TransactionRequestSingle request, ServerCallContext context)
        {
            if (request.Id <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Invalid transaction ID"
                };
            }

            if (request.UserId <= 0)
            {
                return new TransactionCrudResponse
                {
                    Status = 1,
                    ErrorMessage = "Invalid user ID"
                };
            }

            var dto = new CustomerTransactionDto { Id = request.Id };
            var result = await _transactionProcessorService.DeleteTransaction(dto, request.UserId);

            return new TransactionCrudResponse
            {
                Status = !result ? 0 : 1,
                ErrorMessage = result ? "" : "Failed to delete transaction"
            };
        }

        public override async Task<TransactionCrudResponse> TestCustomerTransactionGrpcService(Empty request, ServerCallContext context)
        {
            ConsumerResponseModel response = await _transactionProcessorService.TestCustomerTransactionProcessorService();
            return new TransactionCrudResponse
            {
                Status = response.Status ? 1 : 0,
                ErrorMessage = response.Message
            };
        }
    }
}
