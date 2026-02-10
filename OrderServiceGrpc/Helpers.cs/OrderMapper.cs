using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class OrderMapper
    {
        // ===== DTO to Entity =====

        public static OrderModel DtoToEntity(CreateOrderRequestDto dto)
        {
            if (dto?.Order == null)
                throw new ArgumentNullException(nameof(dto));

            return DtoToEntity(dto.Order);
        }

        public static OrderModel DtoToEntity(OrderDto dto)
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
                OrderItems = dto.Items?.Select(ItemDtoToItemEntity).ToList() ?? new List<OrderItemModel>()
            };
        }

        public static OrderItemModel ItemDtoToItemEntity(OrderItemDto dto)
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

        public static CreateOrderRequestDto ItemEntityToItemDto(OrderModel entity, int userId)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            return new CreateOrderRequestDto
            {
                Order = EntityToOrderDto(entity),
                UserId = userId
            };
        }

        public static OrderDto EntityToOrderDto(OrderModel entity)
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
                Items = entity.OrderItems?.Select(ItemEntityToItemDto).ToList() ?? new List<OrderItemDto>()
            };
        }

        public static OrderItemDto ItemEntityToItemDto(OrderItemModel entity)
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

        // ===== Proto to Entity =====

        public static OrderModel MessageToEntity(Order message)
        {
            if (message == null) return null;

            return new OrderModel
            {
                Id = message.Id,

                OrderDate = message.OrderDate != null
                    ? DateTimeHelper.ConvertTimestampToDateTime(message.OrderDate)
                    : DateTime.MinValue,

                OrderCounter = message.OrderCounter,
                UserId = message.UserId,
                Status = message.Status,
                NetAmount = (decimal)message.NetAmount,

                CreatedBy = message.CreatedBy,
                CreatedDate = message.CreatedDate != null
                    ? DateTimeHelper.ConvertTimestampToDateTime(message.CreatedDate)
                    : DateTime.MinValue,

                ModifiedBy = message.ModifiedBy,
                ModifiedDate = message.ModifiedDate != null
                    ? DateTimeHelper.ConvertTimestampToDateTime(message.ModifiedDate)
                    : DateTime.MinValue,

                IsDeleted = message.IsDeleted,

                OrderItems = (message.Items != null && message.Items.Count > 0)
                    ? message.Items.Select(ItemMessageToItemEntity).ToList()
                    : new List<OrderItemModel>()
            };
        }

        public static OrderItemModel ItemMessageToItemEntity(OrderItem message)
        {
            if (message == null) return null;

            return new OrderItemModel
            {
                Id = message.Id,
                OrderId = message.OrderId,
                ProductId = message.ProductId,
                Quantity = message.Quantity,
                UnitPrice = (decimal)message.GrossAmount,
                Status = message.Status,

                CreatedBy = message.CreatedBy,
                CreatedDate = message.CreatedDate != null
                    ? DateTimeHelper.ConvertTimestampToDateTime(message.CreatedDate)
                    : DateTime.MinValue,

                ModifiedBy = message.ModifiedBy,
                ModifiedDate = message.ModifiedDate != null
                    ? DateTimeHelper.ConvertTimestampToDateTime(message.ModifiedDate)
                    : DateTime.MinValue,

                IsDeleted = message.IsDeleted
            };
        }

        // ===== Entity to Proto =====

        public static Order EntityToMessage(OrderModel model)
        {
            if (model == null) return null;

            var message = new Order
            {
                Id = model.Id,
                OrderDate = DateTimeHelper.ConvertDateTimeToTimestamp(model.OrderDate),
                OrderCounter = model.OrderCounter,
                UserId = model.UserId,
                Status = model.Status,
                NetAmount = (double)model.NetAmount,

                CreatedBy = model.CreatedBy,
                CreatedDate = DateTimeHelper.ConvertDateTimeToTimestamp(model.CreatedDate),
                ModifiedBy = model.ModifiedBy,
                ModifiedDate = DateTimeHelper.ConvertDateTimeToTimestamp(model.ModifiedDate),

                IsDeleted = model.IsDeleted
            };

            // Add items only if available
            if (model.OrderItems != null && model.OrderItems.Count > 0)
            {
                foreach (var item in model.OrderItems)
                {
                    message.Items.Add(ItemEntityToItemMessage(item));
                }
            }

            return message;
        }

        public static OrderItem ItemEntityToItemMessage(OrderItemModel model)
        {
            if (model == null) return null;

            return new OrderItem
            {
                Id = model.Id,
                OrderId = model.OrderId,
                ProductId = model.ProductId,
                Quantity = model.Quantity,
                GrossAmount = (double)model.UnitPrice,
                Status = model.Status,

                CreatedBy = model.CreatedBy,
                CreatedDate = DateTimeHelper.ConvertDateTimeToTimestamp(model.CreatedDate),
                ModifiedBy = model.ModifiedBy,
                ModifiedDate = DateTimeHelper.ConvertDateTimeToTimestamp(model.ModifiedDate),

                IsDeleted = model.IsDeleted
            };
        }

        // ===== Proto to DTO =====

        public static CreateOrderRequestDto CreateOrderRequestToDto(CreateOrderRequest proto)
        {
            if (proto == null)
                throw new ArgumentNullException(nameof(proto));

            return new CreateOrderRequestDto
            {
                Order = ProtoToDto(proto.Order),
                UserId = proto.UserId
            };
        }

        public static OrderDto ProtoToDto(Order proto)
        {
            if (proto == null)
                throw new ArgumentNullException(nameof(proto));

            return new OrderDto
            {
                Id = proto.Id,
                OrderDate = TimestampProtoToDto(proto.OrderDate),
                OrderCounter = proto.OrderCounter,
                UserId = proto.UserId,
                Status = proto.Status ?? "",
                NetAmount = proto.NetAmount,
                CreatedBy = proto.CreatedBy,
                CreatedDate = TimestampProtoToDto(proto.CreatedDate),
                ModifiedBy = proto.ModifiedBy,
                ModifiedDate = TimestampProtoToDto(proto.ModifiedDate),
                IsDeleted = proto.IsDeleted,
                Items = proto.Items?.Select(ItemProtoToDto).ToList() ?? new List<OrderItemDto>()
            };
        }

        public static OrderItemDto ItemProtoToDto(OrderItem proto)
        {
            if (proto == null)
                throw new ArgumentNullException(nameof(proto));

            return new OrderItemDto
            {
                Id = proto.Id,
                OrderId = proto.OrderId,
                ProductId = proto.ProductId,
                Quantity = proto.Quantity,
                GrossAmount = proto.GrossAmount,
                Status = proto.Status ?? "",
                CreatedBy = proto.CreatedBy,
                CreatedDate = TimestampProtoToDto(proto.CreatedDate),
                ModifiedBy = proto.ModifiedBy,
                ModifiedDate = TimestampProtoToDto(proto.ModifiedDate),
                IsDeleted = proto.IsDeleted
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

        private static TimestampDto TimestampProtoToDto(Google.Protobuf.WellKnownTypes.Timestamp proto)
        {
            if (proto == null)
                return new TimestampDto();

            return new TimestampDto
            {
                Seconds = proto.Seconds,
                Nanos = proto.Nanos
            };
        }
    }
}
