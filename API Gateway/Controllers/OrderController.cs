using API_Gateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiGateway.Protos;
namespace API_Gateway.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using ApiGateway.Protos;
    using API_Gateway.Helpers;

    //[Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderGrpcClient _grpcClient;
        private readonly IKafkaEventProducer _orderEvent;
        public OrderController(IOrderGrpcClient grpcClient, IKafkaEventProducer orderEvent)
        {
            _grpcClient = grpcClient;
            _orderEvent = orderEvent;
        }

        // Get userId from JWT token
        private int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> CreateOrder([FromBody] Order order)
        {
            order.UserId = UserId;
            order.CreatedBy = UserId;

            var request = new CreateOrderRequest { Order = order };
            var response = await _grpcClient.CreateOrderAsync(request);
            return Ok(response);
        }

        [HttpGet]
        [Route("get/{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var request = new OrderIdRequest { Id = id };
            var response = await _grpcClient.GetOrderByIdAsync(request);
            return Ok(response);
        }

        [HttpGet]
        [Route("user")]
        public async Task<IActionResult> GetOrdersByUser()
        {
            var request = new UserIdRequest { UserId = UserId };
            var response = await _grpcClient.GetOrdersByUserAsync(request);
            return Ok(response);
        }

        [HttpGet]
        [Route("all")]
        public async Task<IActionResult> GetAllOrders([FromQuery] OrderListRequest request)
        {
            request.UserId = UserId;
            var response = await _grpcClient.GetAllOrdersAsync(request);
            return Ok(response);
        }

        [HttpPut]
        [Route("update")]
        public async Task<IActionResult> UpdateOrder([FromBody] Order order)
        {
            order.UserId = UserId;
            order.ModifiedBy = UserId;

            var request = new UpdateOrderRequest { Order = order };
            var response = await _grpcClient.UpdateOrderAsync(request);
            return Ok(response);
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var request = new DeleteOrderRequest { Id = id };
            var response = await _grpcClient.DeleteOrderAsync(request);
            return Ok(response);
        }

        //Event Driven Approach
        [HttpPost]
        [Route("publish-new-order")]
        public async Task<IActionResult> PublishOrderCreatedEvent()
        {
            var result = await _orderEvent.PublishOrderEvent();
            return Ok(result);
        }
    }

}
