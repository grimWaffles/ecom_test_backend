using API_Gateway.Services;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace API_Gateway.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SystemController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IProductGrpcClient _productService;
        private readonly IOrderGrpcClient _orderService;

        public SystemController(IUserService userService, IProductGrpcClient product, IOrderGrpcClient orderGrpcClient)
        {
            _orderService = orderGrpcClient; _userService = userService; _productService = product;
        }

        [Route("ms-health")]
        [HttpGet]
        public async Task<IActionResult> CheckMicroserviceHealth()
        {
            string userResult = await _userService.TestServiceAsync();
            ProductServiceTestMessage productResult = await _productService.TestProductServiceHealth();
            OrderHealthCheckMessage orderResult = await _orderService.TestOrderServiceHealth();

            return Ok(new
            {
                UserServiceStatus = userResult,
                ProductServiceStatus = productResult.StatusMessage,
                OrderServiceResult = orderResult.Message
            });
        }
    }
}
