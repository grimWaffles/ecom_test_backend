using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class OrderMapper
    {
        // ===== DTO to Entity =====

        public static OrderModel ToEntity(CreateOrderRequestDto dto)
        {
            if (dto?.Order == null)
                throw new ArgumentNullException(nameof(dto));

            return ToEntity(dto.Order);
        }

        public static OrderModel ToEntity(OrderDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return new OrderModel
            {
                Id = dto.Id,
                OrderDate = ConvertToDateTime(dto.OrderDate),
                OrderCounter = dto.OrderCounter,
                UserId = dto.UserId,
                Status = dto.Status ?? "",
                NetAmount = (decimal)dto.NetAmount,
                CreatedBy = dto.CreatedBy,
                CreatedDate = ConvertToDateTime(dto.CreatedDate),
                ModifiedBy = dto.ModifiedBy,
                ModifiedDate = ConvertToDateTime(dto.ModifiedDate),
                IsDeleted = dto.IsDeleted,
                OrderItems = dto.Items?.Select(ToEntity).ToList() ?? new List<OrderItemModel>()
            };
        }

        public static OrderItemModel ToEntity(OrderItemDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            return new OrderItemModel
            {
                Id = dto.Id,
                OrderId = dto.OrderId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                GrossAmount = (decimal)dto.GrossAmount,
                Status = dto.Status ?? "",
                CreatedBy = dto.CreatedBy,
                CreatedDate = ConvertToDateTime(dto.CreatedDate),
                ModifiedBy = dto.ModifiedBy,
                ModifiedDate = ConvertToDateTime(dto.ModifiedDate),
                IsDeleted = dto.IsDeleted,
                UnitPrice = (decimal)dto.UnitPrice
            };
        }

        // ===== Entity to DTO =====

        public static CreateOrderRequestDto ToDto(OrderModel entity, int userId)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new CreateOrderRequestDto
            {
                Order = ToOrderDto(entity),
                UserId = userId
            };
        }

        public static OrderDto ToOrderDto(OrderModel entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new OrderDto
            {
                Id = entity.Id,
                OrderDate = ConvertToTimestampDto(entity.OrderDate),
                OrderCounter = entity.OrderCounter,
                UserId = entity.UserId,
                Status = entity.Status,
                NetAmount = (double)entity.NetAmount,
                CreatedBy = entity.CreatedBy,
                CreatedDate = ConvertToTimestampDto(entity.CreatedDate),
                ModifiedBy = entity.ModifiedBy,
                ModifiedDate = ConvertToTimestampDto(entity.ModifiedDate),
                IsDeleted = entity.IsDeleted,
                Items = entity.OrderItems?.Select(ToDto).ToList() ?? new List<OrderItemDto>()
            };
        }

        public static OrderItemDto ToDto(OrderItemModel entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new OrderItemDto
            {
                Id = entity.Id,
                OrderId = entity.OrderId,
                ProductId = entity.ProductId,
                Quantity = entity.Quantity,
                GrossAmount = (double)entity.GrossAmount,
                Status = entity.Status,
                CreatedBy = entity.CreatedBy,
                CreatedDate = ConvertToTimestampDto(entity.CreatedDate),
                ModifiedBy = entity.ModifiedBy,
                ModifiedDate = ConvertToTimestampDto(entity.ModifiedDate),
                IsDeleted = entity.IsDeleted,
                UnitPrice = (double) entity.UnitPrice
            };
        }

        // ===== Helper Methods =====

        private static DateTime ConvertToDateTime(TimestampDto timestamp)
        {
            if (timestamp == null)
                return DateTime.MinValue;

            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(timestamp.Seconds).AddTicks(timestamp.Nanos / 100);
        }

        private static TimestampDto ConvertToTimestampDto(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var timeSpan = dateTime.ToUniversalTime() - epoch;

            return new TimestampDto
            {
                Seconds = (long)timeSpan.TotalSeconds,
                Nanos = (int)((timeSpan.Ticks % TimeSpan.TicksPerSecond) * 100)
            };
        }
    }
}
