using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;

namespace OrderServiceGrpc.Helpers.Converters
{
    public static class CartMappingExtensions
    {
        public static CartStatusDto ToDto(this CartStatus entity) =>
            new()
            {
                Id = entity.Id,
                CartStatusName = entity.CartStatusName
            };

        public static CartItemStatusDto ToDto(this CartItemStatus entity) =>
            new()
            {
                Id = entity.Id,
                CartItemStatusName = entity.CartItemStatusName
            };

        public static CartItemDto ToDto(this CartItem entity) =>
            new()
            {
                Id = entity.Id,
                CartId = entity.CartId,
                ProductId = entity.ProductId,
                Quantity = entity.Quantity,
                UnitPrice = entity.UnitPrice,
                ReservationExpiresAt = entity.ReservationExpiresAt,
                StatusId = entity.StatusId,
                StatusName = entity.Status?.CartItemStatusName,
                CreatedAt = entity.CreatedAt,
                CreatedBy = entity.CreatedBy,
                UpdatedAt = entity.UpdatedAt,
                UpdatedBy = entity.UpdatedBy
            };

        // Items are filtered by the CartItem query filter (soft delete) when loaded with Include
        public static CartDto ToDto(this Cart entity) =>
            new()
            {
                Id = entity.Id,
                UserId = entity.UserId,
                StatusId = entity.StatusId,
                StatusName = entity.Status?.CartStatusName,
                CreatedAt = entity.CreatedAt,
                CreatedBy = entity.CreatedBy,
                UpdatedAt = entity.UpdatedAt,
                UpdatedBy = entity.UpdatedBy,
                Items = (entity.Items != null) ? entity.Items.Select(i => i.ToDto()).ToList() : new()
            };
    }
}
