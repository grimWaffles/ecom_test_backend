using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Filters;
using API_Gateway.Grpc;
using API_Gateway.Helpers;
using API_Gateway.Models;
using API_Gateway.Models.Dtos;
using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
namespace API_Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(RequirePermissionFilter))]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartGrpcClient _grpcClient;

        public CartController(ICartGrpcClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        [HttpGet]
        [Route("")]
        [RequiresPermission("cart.view")]
        public async Task<IActionResult> ViewCart()
        {
            var request = new ViewCartRequest();
            CartListResponse response = await _grpcClient.ViewCartAsync(request);

            if (!response.Success)
                return BadRequest(new { response.Message });

            List<CartDto> items = response.Items.Select(CustomConverters.CartProtoToDto).ToList();
            return Ok(items);
        }

        [HttpPost]
        [Route("add")]
        [RequiresPermission("cart.create")]
        public async Task<IActionResult> AddToCart([FromBody] CartUpsertDto cart)
        {
            var request = new AddToCartRequest
            {
                Cart = CustomConverters.CartDtoToProto(cart)
            };

            CartResponse response = await _grpcClient.AddToCartAsync(request);

            if (!response.Success)
                return BadRequest(new { response.Message });

            return Ok(CustomConverters.CartProtoToDto(response.Cart));
        }

        [HttpPut]
        [Route("update")]
        [RequiresPermission("cart.update")]
        public async Task<IActionResult> UpdateCartItem([FromBody] CartUpsertDto cart)
        {
            var request = new UpdateCartItemRequest
            {
                Cart = CustomConverters.CartDtoToProto(cart)
            };

            CartResponse response = await _grpcClient.UpdateCartItemAsync(request);

            if (!response.Success)
                return BadRequest(new { response.Message });

            return Ok(CustomConverters.CartProtoToDto(response.Cart));
        }

        [HttpDelete]
        [Route("{cartId}")]
        [RequiresPermission("cart.delete")]
        public async Task<IActionResult> RemoveFromCart(long cartId)
        {
            var request = new RemoveFromCartRequest { CartId = cartId };
            RemoveFromCartResponse response = await _grpcClient.RemoveFromCartAsync(request);

            if (!response.Success)
                return BadRequest(new { response.Message });

            return Ok(new { response.Success });
        }
    }

}