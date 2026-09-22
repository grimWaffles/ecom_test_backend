using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
            var result = await _cartService.GetAllCarts(1, 10);

            return new TestCartResult() { Message = "Test success" };
        }
    }
}
