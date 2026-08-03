
using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Filters;
using API_Gateway.Helpers;
using API_Gateway.Models;
using API_Gateway.Services;
using ApiGateway.Protos;
using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace API_Gateway.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(RequirePermissionFilter))]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly IOrderGrpcClient _grpcClient;
        public OrderController(IOrderGrpcClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        // Get userId from JWT token
        private int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        [HttpPost]
        [Route("create")]
        [RequiresPermission("order.create")]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDto order)
        {
            order.UserId = UserId;
            order.CreatedBy = UserId;

            var request = new CreateOrderRequest { Order = CustomConverters.ModelDtoToProto(order) };
            OrderResponse response = await _grpcClient.CreateOrderAsync(request);

            return Ok(CustomConverters.ResponseProtoToDto(response));
        }

        [HttpGet]
        [Route("get/{id}")]
        [RequiresPermission("order.view")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var request = new OrderIdRequest { Id = id };
            OrderResponse response = await _grpcClient.GetOrderByIdAsync(request);
            return Ok(CustomConverters.ResponseProtoToDto(response));
        }

        [HttpGet]
        [Route("user")]
        [RequiresPermission("order.view")]
        public async Task<IActionResult> GetOrdersByUser([FromQuery] OrderListRequestDto request)
        {
            request.UserId = UserId;

            var r = new OrderListRequest()
            {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                UserId = request.UserId,
                StartDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(request.StartDate),
                EndDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(request.EndDate),
            };

            OrderListResponse response = await _grpcClient.GetOrdersByUserAsync(r);

            return Ok(CustomConverters.ListResponseProtoToDto(response));
        }

        [HttpGet]
        [Route("all")]
        [RequiresPermission("order.view")]
        public async Task<IActionResult> GetAllOrders([FromQuery] OrderListRequestDto request)
        {
            request.UserId = UserId;

            var r = new OrderListRequest()
            {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                UserId = request.UserId,
                StartDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(request.StartDate),
                EndDate = CustomConverters.ConvertDateTimeToGoogleTimeStamp(request.EndDate),
            };

            OrderListResponse response = await _grpcClient.GetAllOrdersAsync(r);

            return Ok(CustomConverters.ListResponseProtoToDto(response));
        }

        [HttpPut]
        [Route("update")]
        [RequiresPermission("order.update")]
        public async Task<IActionResult> UpdateOrder([FromBody] OrderDto order)
        {
            order.UserId = UserId;
            order.ModifiedBy = UserId;

            var request = new UpdateOrderRequest { Order = CustomConverters.ModelDtoToProto(order) };
            OrderResponse response = await _grpcClient.UpdateOrderAsync(request);
            return Ok(CustomConverters.ResponseProtoToDto(response));
        }

        [HttpDelete]
        [Route("delete/{id}")]
        [RequiresPermission("order.delete")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var request = new DeleteOrderRequest { Id = id };
            OrderResponse response = await _grpcClient.DeleteOrderAsync(request);
            return Ok(CustomConverters.ResponseProtoToDto(response));
        }

        [HttpGet]
        [Route("integration-test")]
        [RequiresPermission("order.test")]
        public async Task<IActionResult> TestOrderServiceGrpc()
        {
            OrderResponse response = await _grpcClient.TestOrderServiceAsync(new Google.Protobuf.WellKnownTypes.Empty());

            return Ok(CustomConverters.ResponseProtoToDto(response));
        }

        //Event Driven Approach
        [HttpPost]
        [Route("publish-new-order")]
        [RequiresPermission("order.test")]
        public async Task<IActionResult> PublishOrderCreatedEvent()
        {
            OrderResponse result = await _grpcClient.GenerateCustomManualOrder();
            return Ok(CustomConverters.ResponseProtoToDto(result));
        }
    }
}
