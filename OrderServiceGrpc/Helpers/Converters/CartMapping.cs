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

        //SaveCartDto and SaveCartItemDto
        public static CartItem SaveCartItemDtoToEntity(this SaveCartItemDto x) =>
            new()
            {
                Id = 0,
                CartId = 0,
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = 0,
                ReservationExpiresAt = DateTime.UtcNow,
                StatusId = 0,
                IsDeleted = false,
            };

        public static Cart SaveCartDtoToEntity(this SaveCartDto x) =>
            new()
            {
                Id = x.Id,
                UserId = 0,
                StatusId = 0,
                IsDeleted = false,
                Items = x.Items.Select(i => i.SaveCartItemDtoToEntity()).ToList()
            };
    }
}
