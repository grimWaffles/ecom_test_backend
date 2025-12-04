using Google.Protobuf.WellKnownTypes;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;

namespace OrderServiceGrpc.Helpers.cs
{
    public static class OrderMessageModelConverter
    {
        // -----------------------------
        //  MESSAGE → MODEL CONVERTERS
        // -----------------------------
        public static OrderModel ToModel(Order message)
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
                    ? message.Items.Select(ToModel).ToList()
                    : new List<OrderItemModel>()
            };
        }

        public static OrderItemModel ToModel(OrderItem message)
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

        // -----------------------------
        //  MODEL → MESSAGE CONVERTERS
        // -----------------------------
        public static Order ToMessage(OrderModel model)
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
                    message.Items.Add(ToMessage(item));
                }
            }

            return message;
        }

        public static OrderItem ToMessage(OrderItemModel model)
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
    }
}
