using Microsoft.EntityFrameworkCore.Storage;
using OrderServiceGrpc.Database;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public interface ICartService
    {
        Task<ServiceResult<CartDto>> AddToCartAsync(CartUpsertDto dto, int userId);
        Task<ServiceResult<CartDto>> UpdateCartItemAsync(CartUpsertDto dto, int userId);
        Task<ServiceResult<bool>> RemoveFromCartAsync(long cartId, int userId);
        Task<ServiceResult<List<CartDto>>> ViewCartAsync(int userId);
    }

    public class CartService : ICartService
    {
        private const int ReservationLockMinutes = 15;

        private readonly ICartRepository _cartRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IInventoryReservationRepository _reservationRepository;
        private readonly AppDbContext _context;
        private readonly UnitOfWorkContext _uowContext;
        private readonly ILogger<CartService> _logger;

        public CartService(
            ICartRepository cartRepository,
            IInventoryRepository inventoryRepository,
            IInventoryReservationRepository reservationRepository,
            AppDbContext context,
            UnitOfWorkContext uowContext,
            ILogger<CartService> logger)
        {
            _cartRepository = cartRepository;
            _inventoryRepository = inventoryRepository;
            _reservationRepository = reservationRepository;
            _context = context;
            _uowContext = uowContext;
            _logger = logger;
        }

        public async Task<ServiceResult<CartDto>> AddToCartAsync(CartUpsertDto dto, int userId)
        {
            if (dto.Quantity <= 0)
                return ServiceResult<CartDto>.Fail("Quantity must be greater than 0.");

            _uowContext.IsUnderUnitOfWork = true;
            IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Cart? existingCart = await _cartRepository.GetByUserIdAndProductIdAsync(dto.UserId, dto.ProductId);

                ServiceResult<Cart> result = existingCart is not null
                    ? await UpsertQuantityAsync(existingCart, dto, userId)
                    : await InsertNewCartItemAsync(dto, userId);

                if (!result.Success)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<CartDto>.Fail(result.Message!);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Added/updated ProductId: {ProductId} in Cart for UserId: {UserId}", dto.ProductId, dto.UserId);

                return ServiceResult<CartDto>.SuccessResult(MapToDto(result.Data!));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adding ProductId: {ProductId} to Cart for UserId: {UserId}", dto.ProductId, dto.UserId);
                throw;
            }
            finally
            {
                _uowContext.IsUnderUnitOfWork = false;
            }
        }

        public async Task<ServiceResult<CartDto>> UpdateCartItemAsync(CartUpsertDto dto, int userId)
        {
            if (dto.Quantity <= 0)
                return ServiceResult<CartDto>.Fail("Quantity must be greater than 0.");

            _uowContext.IsUnderUnitOfWork = true;
            IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Cart? existingCart = await _cartRepository.GetByIdAsync(dto.Id);
                if (existingCart is null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<CartDto>.Fail("Cart item not found.");
                }

                ServiceResult<Cart> result = await UpsertQuantityAsync(existingCart, dto, userId);

                if (!result.Success)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<CartDto>.Fail(result.Message!);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Updated Cart Id: {Id} to Quantity: {Quantity}", dto.Id, dto.Quantity);

                return ServiceResult<CartDto>.SuccessResult(MapToDto(result.Data!));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating Cart Id: {Id}", dto.Id);
                throw;
            }
            finally
            {
                _uowContext.IsUnderUnitOfWork = false;
            }
        }

        public async Task<ServiceResult<bool>> RemoveFromCartAsync(long cartId, int userId)
        {
            if (cartId <= 0)
                return ServiceResult<bool>.Fail("CartId must be greater than 0.");

            _uowContext.IsUnderUnitOfWork = true;
            IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                Cart? existingCart = await _cartRepository.GetByIdAsync(cartId);
                if (existingCart is null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<bool>.Fail("Cart item not found.");
                }

                if (existingCart.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<bool>.Fail("You do not have permission to remove this cart item.");
                }

                InventoryReservation? reservation = await _reservationRepository.GetByCartIdAsync(cartId);

                bool cartDeleted = await _cartRepository.DeleteAsync(cartId, userId);
                if (!cartDeleted)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult<bool>.Fail("Failed to remove cart item.");
                }

                if (reservation is not null)
                {
                    await _reservationRepository.DeleteAsync(reservation.Id, userId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Removed Cart Id: {Id} and released its reservation", cartId);

                return ServiceResult<bool>.SuccessResult(true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error removing Cart Id: {Id}", cartId);
                throw;
            }
            finally
            {
                _uowContext.IsUnderUnitOfWork = false;
            }
        }

        public async Task<ServiceResult<List<CartDto>>> ViewCartAsync(int userId)
        {
            if (userId <= 0)
                return ServiceResult<List<CartDto>>.Fail("UserId must be greater than 0.");

            List<Cart> cartItems = await _cartRepository.GetByUserIdAsync(userId);

            List<CartDto> dtos = cartItems.Select(MapToDto).ToList();

            return ServiceResult<List<CartDto>>.SuccessResult(dtos);
        }

        // ===================== Private helpers =====================

        private async Task<ServiceResult<Cart>> InsertNewCartItemAsync(CartUpsertDto dto, int userId)
        {
            var inventoryList = await _inventoryRepository.GetByProductIdAsync(dto.ProductId);
            Inventory? inventory = inventoryList.FirstOrDefault();

            if (inventory is null)
                return ServiceResult<Cart>.Fail("Inventory record not found for this product.");

            int lockedQuantity = await _reservationRepository.GetActiveLockedQuantityByProductIdAsync(dto.ProductId);
            int availableQuantity = inventory.Quantity - lockedQuantity;

            if (availableQuantity < dto.Quantity)
                return ServiceResult<Cart>.Fail($"Insufficient stock. Only {availableQuantity} unit(s) available.");

            Cart cartEntity = new Cart
            {
                UserId = dto.UserId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice
            };

            Cart createdCart = await _cartRepository.CreateAsync(cartEntity, userId);

            InventoryReservation reservation = new InventoryReservation
            {
                ProductId = dto.ProductId,
                CartId = createdCart.Id,
                LockQuantity = dto.Quantity,
                LockExpirationDate = DateTime.UtcNow.AddMinutes(ReservationLockMinutes)
            };

            await _reservationRepository.CreateAsync(reservation, userId);

            return ServiceResult<Cart>.SuccessResult(createdCart);
        }

        private async Task<ServiceResult<Cart>> UpsertQuantityAsync(Cart existingCart, CartUpsertDto dto, int userId)
        {
            InventoryReservation? reservation = await _reservationRepository.GetByCartIdAsync(existingCart.Id);
            if (reservation is null)
                return ServiceResult<Cart>.Fail("Associated inventory reservation not found.");

            int quantityDiff = dto.Quantity - existingCart.Quantity;

            if (quantityDiff > 0)
            {
                var inventoryList = await _inventoryRepository.GetByProductIdAsync(dto.ProductId);
                Inventory? inventory = inventoryList.FirstOrDefault();

                if (inventory is null)
                    return ServiceResult<Cart>.Fail("Inventory record not found for this product.");

                int lockedQuantity = await _reservationRepository.GetActiveLockedQuantityByProductIdAsync(
                    dto.ProductId, excludeReservationId: reservation.Id);

                int availableQuantity = inventory.Quantity - lockedQuantity;

                if (availableQuantity < dto.Quantity)
                    return ServiceResult<Cart>.Fail($"Insufficient stock. Only {availableQuantity} unit(s) available.");
            }

            existingCart.Quantity = dto.Quantity;
            existingCart.UnitPrice = dto.UnitPrice;
            Cart? updatedCart = await _cartRepository.UpdateAsync(existingCart, userId);

            reservation.LockQuantity = dto.Quantity;
            await _reservationRepository.UpdateAsync(reservation, userId);

            return ServiceResult<Cart>.SuccessResult(updatedCart!);
        }

        private static CartDto MapToDto(Cart entity)
        {
            return new CartDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                ProductId = entity.ProductId,
                Quantity = entity.Quantity,
                UnitPrice = entity.UnitPrice,
                CreatedBy = entity.CreatedBy,
                CreatedDate = entity.CreatedDate,
                IsDeleted = entity.IsDeleted
            };
        }
    }
}
