using API_Gateway.Models;
using ApiGateway.Protos;
using Google.Protobuf.WellKnownTypes;

namespace API_Gateway.Helpers
{
    public static class CustomConverters
    {
        public static Timestamp ConvertDateTimeToGoogleTimeStamp(DateTime date)
        {
            return Timestamp.FromDateTime(DateTime.SpecifyKind(date, DateTimeKind.Utc)) ;
        }

        // Proto -> DTO
        public static OrderDto ModelProtoToDto(this Order proto)
        {
            return new OrderDto
            {
                Id = proto.Id,
                OrderDate = proto.OrderDate?.ToDateTime(),
                OrderCounter = proto.OrderCounter,
                UserId = proto.UserId,
                Status = proto.Status,
                NetAmount = (decimal)proto.NetAmount,
                CreatedBy = proto.CreatedBy,
                CreatedDate = proto.CreatedDate?.ToDateTime(),
                ModifiedDate = proto.ModifiedDate?.ToDateTime(),
                ModifiedBy = proto.ModifiedBy,
                IsDeleted = proto.IsDeleted,

                Items = proto.Items
                    .Select(x => x.ItemProtoToDto())
                    .ToList()
            };
        }

        public static OrderItemDto ItemProtoToDto(this OrderItem proto)
        {
            return new OrderItemDto
            {
                Id = proto.Id,
                OrderId = proto.OrderId,
                ProductId = proto.ProductId,
                Quantity = proto.Quantity,
                GrossAmount = (decimal)proto.GrossAmount,
                Status = proto.Status,
                CreatedBy = proto.CreatedBy,
                CreatedDate = proto.CreatedDate?.ToDateTime(),
                ModifiedBy = proto.ModifiedBy,
                ModifiedDate = proto.ModifiedDate?.ToDateTime(),
                IsDeleted = proto.IsDeleted,
                UnitPrice = (decimal)proto.UnitPrice
            };
        }

        // DTO -> Proto
        public static Order ModelDtoToProto(this OrderDto dto)
        {
            var order = new Order
            {
                Id = dto.Id,
                OrderCounter = dto.OrderCounter,
                UserId = dto.UserId,
                Status = dto.Status,
                NetAmount = (double)dto.NetAmount,
                CreatedBy = dto.CreatedBy,
                ModifiedBy = dto.ModifiedBy,
                IsDeleted = dto.IsDeleted
            };

            if (dto.OrderDate.HasValue)
                order.OrderDate = Timestamp.FromDateTime(dto.OrderDate.Value.ToUniversalTime());

            if (dto.CreatedDate.HasValue)
                order.CreatedDate = Timestamp.FromDateTime(dto.CreatedDate.Value.ToUniversalTime());

            if (dto.ModifiedDate.HasValue)
                order.ModifiedDate = Timestamp.FromDateTime(dto.ModifiedDate.Value.ToUniversalTime());


            order.Items.AddRange(
                dto.Items.Select(x => x.ItemModelToProto())
            );

            return order;
        }

        public static OrderItem ItemModelToProto(this OrderItemDto dto)
        {
            var item = new OrderItem
            {
                Id = dto.Id,
                OrderId = dto.OrderId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                GrossAmount = (double)dto.GrossAmount,
                Status = dto.Status,
                CreatedBy = dto.CreatedBy,
                ModifiedBy = dto.ModifiedBy,
                IsDeleted = dto.IsDeleted,
                UnitPrice = (double)dto.UnitPrice
            };


            if (dto.CreatedDate.HasValue)
                item.CreatedDate = Timestamp.FromDateTime(dto.CreatedDate.Value.ToUniversalTime());

            if (dto.ModifiedDate.HasValue)
                item.ModifiedDate = Timestamp.FromDateTime(dto.ModifiedDate.Value.ToUniversalTime());


            return item;
        }

        // Response Proto -> Response Dto
        public static OrderResponseDto ResponseProtoToDto(this OrderResponse response)
        {
            return new OrderResponseDto
            {
                Status = response.Status,
                Message = response.Message,
                Order = response.Order != null
                    ? response.Order.ModelProtoToDto()
                    : null
            };
        }

        public static OrderListResponseDto ListResponseProtoToDto(this OrderListResponse response)
        {
            return new OrderListResponseDto
            {
                Status = response.Status,
                Message = response.Message,
                TotalPages = response.TotalPages,
                TotalOrders = response.TotalOrders,

                Orders = response.Orders
                    .Select(order => order.ModelProtoToDto())
                    .ToList()
            };
        }
    }
}
