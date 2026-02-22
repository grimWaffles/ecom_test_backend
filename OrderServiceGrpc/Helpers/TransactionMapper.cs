
using Google.Protobuf.WellKnownTypes;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;

namespace OrderServiceGrpc.Helpers
{
    public static class TransactionMapper
    {
        public static TransactionDto ToDto(CustomerTransactionModel model)
        {
            return new TransactionDto
            {
                Id = model.Id,
                UserId = model.UserId,
                TransactionType = model.TransactionType ?? string.Empty,
                Amount = (double)model.Amount,
                CreatedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(model.CreatedDate, DateTimeKind.Utc)),
                CreatedBy = model.CreatedBy,
                IsDeleted = model.IsDeleted,
                TransactionDate = Timestamp.FromDateTime(DateTime.SpecifyKind(model.TransactionDate, DateTimeKind.Utc)),
                ModifiedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(model.ModifiedDate, DateTimeKind.Utc)),
                ModifiedBy = model.ModifiedBy,
                TransactionKey = model.TransactionKey?.Length > 15
                    ? model.TransactionKey[..15]
                    : model.TransactionKey ?? string.Empty
            };
        }

        public static CustomerTransactionModel ToEntity(TransactionDto dto)
        {
            return new CustomerTransactionModel
            {
                Id = (int)dto.Id,
                UserId = (int)dto.UserId,
                TransactionType = dto.TransactionType,
                Amount = (decimal)dto.Amount,
                CreatedDate = dto.CreatedDate?.ToDateTime() ?? DateTime.UtcNow,
                CreatedBy = (int)dto.CreatedBy,
                IsDeleted = dto.IsDeleted,
                TransactionDate = dto.TransactionDate?.ToDateTime() ?? DateTime.UtcNow,
                ModifiedDate = dto.ModifiedDate?.ToDateTime() ?? DateTime.UtcNow,
                ModifiedBy = dto.ModifiedBy,
                TransactionKey = dto.TransactionKey?.Length > 15
                    ? dto.TransactionKey[..15]
                    : dto.TransactionKey
            };
        }
    }
}
