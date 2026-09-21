using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using OrderServiceGrpc.Authorization;
using OrderServiceGrpc.Helpers;
using OrderServiceGrpc.Models;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Services;
using System.Globalization;
using System.Security.Claims;

namespace OrderServiceGrpc.GrpcServices
{
    [Authorize]
    public class CartGrpcService : OrderServiceGrpc.Protos.CartService.CartServiceBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartGrpcService> _logger;

        public CartGrpcService(
            ICartService cartService,
            ILogger<CartGrpcService> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        [RequiresPermission("cart.create")]
        public override async Task<CartResponse> AddToCart(
            AddToCartRequest request, ServerCallContext context)
        {
            if (request.Cart is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Cart payload is required."));

            int userId = request.Cart.UserId;

            ValidateUpsertMessage(request.Cart);

            CartUpsertDto dto = MapToUpsertDto(request.Cart, userId);

            ServiceResult<CartDto> result = await _cartService.AddToCartAsync(dto, userId);

            return new CartResponse
            {
                Success = result.Success,
                Message = result.Message ?? string.Empty,
                Cart = result.Success && result.Data is not null ? MapToMessage(result.Data) : null
            };
        }

        [RequiresPermission("cart.update")]
        public override async Task<CartResponse> UpdateCartItem(
            UpdateCartItemRequest request, ServerCallContext context)
        {
            if (request.Cart is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Cart payload is required."));

            if (request.Cart.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0 for update."));

            int userId = request.Cart.UserId;

            ValidateUpsertMessage(request.Cart);

            CartUpsertDto dto = MapToUpsertDto(request.Cart, userId);

            ServiceResult<CartDto> result = await _cartService.UpdateCartItemAsync(dto, userId);

            return new CartResponse
            {
                Success = result.Success,
                Message = result.Message ?? string.Empty,
                Cart = result.Success && result.Data is not null ? MapToMessage(result.Data) : null
            };
        }

        [RequiresPermission("cart.delete")]
        public override async Task<RemoveFromCartResponse> RemoveFromCart(
            RemoveFromCartRequest request, ServerCallContext context)
        {
            if (request.CartId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "CartId must be greater than 0."));

            int userId = request.UserId;

            ServiceResult<bool> result = await _cartService.RemoveFromCartAsync(request.CartId, userId);

            return new RemoveFromCartResponse
            {
                Success = result.Success,
                Message = result.Message ?? string.Empty
            };
        }

        [RequiresPermission("cart.view")]
        public override async Task<CartListResponse> ViewCart(
            ViewCartRequest request, ServerCallContext context)
        {
            int userId = request.UserId;

            ServiceResult<List<CartDto>> result = await _cartService.ViewCartAsync(userId);

            CartListResponse response = new CartListResponse
            {
                Success = result.Success,
                Message = result.Message ?? string.Empty
            };

            if (result.Success && result.Data is not null)
            {
                response.Items.AddRange(result.Data.Select(MapToMessage));
            }

            return response;
        }

        // ===================== Validation =====================

        private static void ValidateUpsertMessage(CartUpsertMessage message)
        {
            if (message.ProductId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be greater than 0."));

            if (message.Quantity <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity must be greater than 0."));

            if (string.IsNullOrWhiteSpace(message.UnitPrice) ||
                !decimal.TryParse(message.UnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsedPrice) ||
                parsedPrice < 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UnitPrice must be a valid non-negative decimal value."));
            }
        }

        // ===================== Message <-> Dto Conversions =====================

        private static CartMessage MapToMessage(CartDto dto)
        {
            return new CartMessage
            {
                Id = dto.Id,
                UserId = dto.UserId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice.ToString(CultureInfo.InvariantCulture),
                CreatedBy = dto.CreatedBy,
                CreatedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.CreatedDate, DateTimeKind.Utc)),
                IsDeleted = dto.IsDeleted
            };
        }

        private static CartUpsertDto MapToUpsertDto(CartUpsertMessage message, int userId)
        {
            return new CartUpsertDto
            {
                Id = message.Id,
                UserId = userId,
                ProductId = message.ProductId,
                Quantity = message.Quantity,
                UnitPrice = decimal.Parse(message.UnitPrice, NumberStyles.Number, CultureInfo.InvariantCulture)
            };
        }
    }
}
