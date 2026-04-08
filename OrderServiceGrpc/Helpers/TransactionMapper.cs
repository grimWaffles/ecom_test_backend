
using Google.Protobuf.WellKnownTypes;
using OrderServiceGrpc.Models.Dtos;
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
                    : model.TransactionKey ?? string.Empty,
                OrderId = model.OrderId,
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
                    : dto.TransactionKey,
                OrderId = dto.OrderId,
            };
        }

        //Entity <--> DTO
        public static CustomerTransactionDto EntityToDto(CustomerTransactionModel entity)
        {
            return new CustomerTransactionDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                TransactionType = entity.TransactionType,
                Amount = entity.Amount,
                CreatedDate = entity.CreatedDate,
                CreatedBy = entity.CreatedBy,
                IsDeleted = entity.IsDeleted,
                TransactionDate = entity.TransactionDate,
                ModifiedDate = entity.ModifiedDate,
                ModifiedBy = entity.ModifiedBy,
                TransactionKey = entity.TransactionKey,
                OrderId = entity.OrderId,
            };
        }

        public static CustomerTransactionModel DtoToEntity(CustomerTransactionDto dto)
        {
            return new CustomerTransactionModel
            {
                Id = dto.Id,
                UserId = dto.UserId,
                TransactionType = dto.TransactionType,
                Amount = dto.Amount,
                CreatedDate = dto.CreatedDate,
                CreatedBy = dto.CreatedBy,
                IsDeleted = dto.IsDeleted,
                TransactionDate = dto.TransactionDate,
                ModifiedDate = dto.ModifiedDate,
                ModifiedBy = dto.ModifiedBy,
                TransactionKey = dto.TransactionKey,
                OrderId = dto.OrderId,
            };
        }

        //DTO <--> Proto
        public static TransactionDto DtoToProto(CustomerTransactionDto dto)
        {
            return new TransactionDto
            {
                Id = dto.Id,
                UserId = dto.UserId,
                TransactionType = dto.TransactionType ?? string.Empty,
                Amount = (double)dto.Amount,
                CreatedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.CreatedDate, DateTimeKind.Utc)),
                CreatedBy = dto.CreatedBy,
                IsDeleted = dto.IsDeleted,
                TransactionDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.TransactionDate, DateTimeKind.Utc)),
                ModifiedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.ModifiedDate, DateTimeKind.Utc)),
                ModifiedBy = dto.ModifiedBy,
                TransactionKey = dto.TransactionKey ?? string.Empty,
                OrderId = dto.OrderId,
            };
        }

        public static CustomerTransactionDto ProtoToDto(TransactionDto proto)
        {
            return new CustomerTransactionDto
            {
                Id = (int)proto.Id,
                UserId = (int)proto.UserId,
                TransactionType = proto.TransactionType,
                Amount = (decimal)proto.Amount,
                CreatedDate = proto.CreatedDate?.ToDateTime() ?? DateTime.UtcNow,
                CreatedBy = (int)proto.CreatedBy,
                IsDeleted = proto.IsDeleted,
                TransactionDate = proto.TransactionDate?.ToDateTime() ?? DateTime.UtcNow,
                ModifiedDate = proto.ModifiedDate?.ToDateTime() ?? DateTime.UtcNow,
                ModifiedBy = proto.ModifiedBy,
                TransactionKey = proto.TransactionKey,
                OrderId = proto.OrderId,
            };
        }
    }
}
