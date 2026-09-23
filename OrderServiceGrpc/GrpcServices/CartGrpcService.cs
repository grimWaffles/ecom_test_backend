using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using OrderServiceGrpc.Helpers.Converters;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Services;

namespace OrderServiceGrpc.GrpcServices
{
    public class CartGrpcService : Protos.CartService.CartServiceBase
    {
        private readonly ICartService _cartService;

        public CartGrpcService(ICartService cartService)
        {
            _cartService = cartService;
        }

        public override async Task<TestCartResult> TestCartFunctions(Empty request, ServerCallContext context)
        {
            //int userId = 1;

            //List<SaveCartItemDto> itemList = new List<SaveCartItemDto>
            //{
            //    new SaveCartItemDto { ProductId = 46, Quantity = 13 },
            //    new SaveCartItemDto { ProductId = 47, Quantity = 13 },
            //    new SaveCartItemDto { ProductId = 9,  Quantity = 23 },
            //    new SaveCartItemDto { ProductId = 8,  Quantity = 15 },
            //    //new SaveCartItemDto { ProductId = 53, Quantity = 74 },
            //    //new SaveCartItemDto { ProductId = 81, Quantity = 8 },
            //};

            //SaveCartDto cartDto = new SaveCartDto()
            //{
            //    Id = 3,
            //    Items = itemList
            //};

            //var result = await _cartService.ModifyCart(cartDto, userId);
            //var result2 = await _cartService.GetCartByUserId(1);

            return new TestCartResult() { Message = "Test success" };
        }
    }
}
