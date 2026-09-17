using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Filters;
using API_Gateway.Grpc;
using API_Gateway.Helpers;
using API_Gateway.Models.Dtos;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API_Gateway.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ServiceFilter(typeof(RequirePermissionFilter))]
    [Authorize]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryGrpcClient _grpcClient;

        public InventoryController(IInventoryGrpcClient grpcClient)
        {
            _grpcClient = grpcClient;
        }

        private int UserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        [HttpGet]
        [Route("")]
        [RequiresPermission("inventory.view")]
        public async Task<IActionResult> GetAllInventory([FromQuery] int pageNumber, [FromQuery] int pageSize)
        {
            var request = new GetAllInventoryRequest { PageNumber = pageNumber, PageSize = pageSize };
            InventoryListResponse response = await _grpcClient.GetAllInventoryAsync(request);

            List<InventoryDto> items = response.Items.Select(CustomConverters.InventoryProtoToDto).ToList();
            return Ok(items);
        }

        [HttpGet]
        [Route("product/{productId}")]
        [RequiresPermission("inventory.view")]
        public async Task<IActionResult> GetInventoryByProductId(int productId)
        {
            var request = new GetInventoryByProductIdRequest { ProductId = productId };
            InventoryListResponse response = await _grpcClient.GetInventoryByProductIdAsync(request);

            List<InventoryDto> items = response.Items.Select(CustomConverters.InventoryProtoToDto).ToList();
            return Ok(items);
        }

        [HttpGet]
        [Route("category/{productCategoryId}")]
        [RequiresPermission("inventory.view")]
        public async Task<IActionResult> GetInventoryByProductCategory(
            int productCategoryId, [FromQuery] int pageNumber, [FromQuery] int pageSize)
        {
            var request = new GetInventoryByProductCategoryRequest
            {
                ProductCategoryId = productCategoryId,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
            InventoryListResponse response = await _grpcClient.GetInventoryByProductCategoryAsync(request);

            List<InventoryDto> items = response.Items.Select(CustomConverters.InventoryProtoToDto).ToList();
            return Ok(items);
        }

        [HttpPost]
        [Route("create")]
        [RequiresPermission("inventory.create")]
        public async Task<IActionResult> CreateInventory([FromBody] InventoryUpsertDto inventory)
        {
            var request = new CreateInventoryRequest
            {
                Inventory = CustomConverters.InventoryDtoToProto(inventory),
                UserId = UserId
            };

            InventoryResponse response = await _grpcClient.CreateInventoryAsync(request);

            return Ok(CustomConverters.InventoryProtoToDto(response.Inventory));
        }

        [HttpPut]
        [Route("update")]
        [RequiresPermission("inventory.update")]
        public async Task<IActionResult> UpdateInventory([FromBody] InventoryUpsertDto inventory)
        {
            var request = new UpdateInventoryRequest
            {
                Inventory = CustomConverters.InventoryDtoToProto(inventory),
                UserId = UserId
            };

            InventoryResponse response = await _grpcClient.UpdateInventoryAsync(request);

            return Ok(CustomConverters.InventoryProtoToDto(response.Inventory));
        }

        [HttpDelete]
        [Route("{id}")]
        [RequiresPermission("inventory.delete")]
        public async Task<IActionResult> DeleteInventory(int id)
        {
            var request = new DeleteInventoryRequest { Id = id, UserId = UserId };
            DeleteInventoryResponse response = await _grpcClient.DeleteInventoryAsync(request);

            return Ok(new { response.Success });
        }
    }
}
