using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using OrderServiceGrpc.Authorization;
using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Protos;
using OrderServiceGrpc.Services;

namespace OrderServiceGrpc.GrpcServices
{
    [Authorize]
    public class InventoryGrpcService : Protos.InventoryService.InventoryServiceBase
    {
        private readonly IInventoryService _inventoryService;

        public InventoryGrpcService(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [RequiresPermission("inventory.view")]
        public override async Task<InventoryListResponse> GetAllInventory(
            GetAllInventoryRequest request, ServerCallContext context)
        {
            ValidatePaging(request.PageNumber, request.PageSize);

            List<InventoryDto> dtos = await _inventoryService.GetAllAsync(request.PageNumber, request.PageSize);

            return new InventoryListResponse
            {
                Items = { dtos.Select(MapToMessage) }
            };
        }

        [RequiresPermission("inventory.view")]
        public override async Task<InventoryListResponse> GetInventoryByProductId(
            GetInventoryByProductIdRequest request, ServerCallContext context)
        {
            if (request.ProductId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be greater than 0."));

            List<InventoryDto> dtos = await _inventoryService.GetByProductIdAsync(request.ProductId);

            return new InventoryListResponse
            {
                Items = { dtos.Select(MapToMessage) }
            };
        }

        [RequiresPermission("inventory.create")]
        public override async Task<InventoryResponse> CreateInventory(
            CreateInventoryRequest request, ServerCallContext context)
        {
            if (request.Inventory is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Inventory payload is required."));

            if (request.UserId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UserId must be greater than 0."));

            ValidateUpsertMessage(request.Inventory, isCreate: true);

            InventoryUpsertDto dto = MapToUpsertDto(request.Inventory);

            InventoryDto created = await _inventoryService.CreateAsync(dto, request.UserId);

            return new InventoryResponse
            {
                Inventory = MapToMessage(created)
            };
        }

        [RequiresPermission("inventory.update")]
        public override async Task<InventoryResponse> UpdateInventory(
            UpdateInventoryRequest request, ServerCallContext context)
        {
            if (request.Inventory is null)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Inventory payload is required."));

            if (request.UserId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UserId must be greater than 0."));

            ValidateUpsertMessage(request.Inventory, isCreate: false);

            InventoryUpsertDto dto = MapToUpsertDto(request.Inventory);

            InventoryDto? updated = await _inventoryService.UpdateAsync(dto, request.UserId);

            if (updated is null)
                throw new RpcException(new Status(StatusCode.NotFound, $"Inventory with Id {dto.Id} not found."));

            return new InventoryResponse
            {
                Inventory = MapToMessage(updated)
            };
        }

        [RequiresPermission("inventory.delete")]
        public override async Task<DeleteInventoryResponse> DeleteInventory(
            DeleteInventoryRequest request, ServerCallContext context)
        {
            if (request.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0."));

            if (request.UserId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UserId must be greater than 0."));

            bool deleted = await _inventoryService.DeleteAsync(request.Id, request.UserId);

            if (!deleted)
                throw new RpcException(new Status(StatusCode.NotFound, $"Inventory with Id {request.Id} not found."));

            return new DeleteInventoryResponse
            {
                Success = true
            };
        }

        [RequiresPermission("inventory.view")]
        public override async Task<InventoryListResponse> GetInventoryByProductCategory(
            GetInventoryByProductCategoryRequest request, ServerCallContext context)
        {
            if (request.ProductCategoryId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductCategoryId must be greater than 0."));

            ValidatePaging(request.PageNumber, request.PageSize);

            List<InventoryDto> dtos = await _inventoryService.GetByProductCategoryAsync(
                request.ProductCategoryId, request.PageNumber, request.PageSize);

            return new InventoryListResponse
            {
                Items = { dtos.Select(MapToMessage) }
            };
        }

        [RequiresPermission("inventory.test")]
        public override async Task<RunInventoryLifecycleResponse> RunInventoryLifecycle(
            RunInventoryLifecycleRequest request, ServerCallContext context)
        {
            if (request.UserId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "UserId must be greater than 0."));

            // Step 1: Load all current inventory
            List<InventoryDto> allItems = await _inventoryService.GetAllAsync(pageNumber: 1, pageSize: 100);

            if (allItems.Count == 0)
                throw new RpcException(new Status(StatusCode.FailedPrecondition, "Inventory table is empty — cannot run lifecycle."));

            InventoryDto originalItem = allItems.First();
            int originalCount = allItems.Count;

            // Step 2: Remove one and save
            bool deleteSucceeded = await _inventoryService.DeleteAsync((int)originalItem.Id, request.UserId);

            if (!deleteSucceeded)
                throw new RpcException(new Status(StatusCode.Internal, $"Failed to delete Inventory Id {originalItem.Id}."));

            List<InventoryDto> postDeleteItems = await _inventoryService.GetAllAsync(pageNumber: 1, pageSize: 100);

            // Step 3: Add that one again
            InventoryUpsertDto recreateDto = new InventoryUpsertDto
            {
                ProductId = originalItem.ProductId,
                ProductCategoryId = originalItem.ProductCategoryId,
                Quantity = originalItem.Quantity
            };

            InventoryDto recreatedItem = await _inventoryService.CreateAsync(recreateDto, request.UserId);

            // Step 4: Update its quantity by 40%
            int increasedQuantity = (int)Math.Round(recreatedItem.Quantity * 1.4, MidpointRounding.AwayFromZero);

            InventoryUpsertDto updateDto = new InventoryUpsertDto
            {
                Id = recreatedItem.Id,
                ProductId = recreatedItem.ProductId,
                ProductCategoryId = recreatedItem.ProductCategoryId,
                Quantity = increasedQuantity
            };

            InventoryDto? updatedItem = await _inventoryService.UpdateAsync(updateDto, request.UserId);

            if (updatedItem is null)
                throw new RpcException(new Status(StatusCode.Internal, $"Failed to update recreated Inventory Id {recreatedItem.Id}."));

            return new RunInventoryLifecycleResponse
            {
                OriginalItem = MapToMessage(originalItem),
                DeleteSucceeded = deleteSucceeded,
                OriginalListCount = originalCount,
                PostDeleteListCount = postDeleteItems.Count,
                RecreatedItem = MapToMessage(recreatedItem),
                UpdatedItem = MapToMessage(updatedItem)
            };
        }

        // ===================== Validation =====================

        private static void ValidatePaging(int pageNumber, int pageSize)
        {
            if (pageNumber <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PageNumber must be greater than 0."));

            if (pageSize <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PageSize must be greater than 0."));
        }

        private static void ValidateUpsertMessage(InventoryUpsertMessage message, bool isCreate)
        {
            if (!isCreate && message.Id <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id must be greater than 0 for update."));

            if (message.ProductId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductId must be greater than 0."));

            if (message.ProductCategoryId <= 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "ProductCategoryId must be greater than 0."));

            if (message.Quantity < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Quantity cannot be negative."));
        }

        // ===================== Message <-> Dto Conversions =====================

        private static InventoryMessage MapToMessage(InventoryDto dto)
        {
            InventoryMessage message = new InventoryMessage
            {
                Id = dto.Id,
                ProductId = dto.ProductId,
                ProductCategoryId = dto.ProductCategoryId,
                Quantity = dto.Quantity,
                CreatedBy = dto.CreatedBy,
                CreatedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.CreatedDate, DateTimeKind.Utc)),
                IsDeleted = dto.IsDeleted
            };

            if (dto.ModifiedBy.HasValue)
                message.ModifiedBy = dto.ModifiedBy.Value;

            if (dto.ModifiedDate.HasValue)
                message.ModifiedDate = Timestamp.FromDateTime(DateTime.SpecifyKind(dto.ModifiedDate.Value, DateTimeKind.Utc));

            return message;
        }

        private static InventoryUpsertDto MapToUpsertDto(InventoryUpsertMessage message)
        {
            return new InventoryUpsertDto
            {
                Id = message.Id,
                ProductId = message.ProductId,
                ProductCategoryId = message.ProductCategoryId,
                Quantity = message.Quantity
            };
        }
    }
}
