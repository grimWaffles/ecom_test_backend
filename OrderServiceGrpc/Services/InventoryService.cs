using OrderServiceGrpc.Models.Dtos;
using OrderServiceGrpc.Models.Entities;
using OrderServiceGrpc.Repository;

namespace OrderServiceGrpc.Services
{
    public interface IInventoryService
    {
        Task<List<InventoryDto>> GetAllAsync(int pageNumber, int pageSize, bool track = false);
        Task<List<InventoryDto>> GetByProductIdAsync(int productId, bool track = false);
        Task<InventoryDto> CreateAsync(InventoryUpsertDto dto, int userId);
        Task<InventoryDto?> UpdateAsync(InventoryUpsertDto dto, int userId);
        Task<bool> DeleteAsync(int id, int userId);
        Task<List<InventoryDto>> GetByProductCategoryAsync(int productCategoryId, int pageNumber, int pageSize, bool track = false);
    }

    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _repository;
        private readonly ILogger<InventoryService> _logger;

        public InventoryService(
            IInventoryRepository repository,
            ILogger<InventoryService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<List<InventoryDto>> GetAllAsync(int pageNumber, int pageSize, bool track = false)
        {
            _logger.LogInformation("Getting Inventory — PageNumber: {PageNumber}, PageSize: {PageSize}", pageNumber, pageSize);

            List<Inventory> entities = await _repository.GetAllAsync(pageNumber, pageSize, track);

            return entities.Select(MapToDto).ToList();
        }

        public async Task<List<InventoryDto>> GetByProductIdAsync(int productId, bool track = false)
        {
            _logger.LogInformation("Getting Inventory with ProductId: {ProductId}", productId);

            List<Inventory> entities = await _repository.GetByProductIdAsync(productId, track);

            return entities.Select(MapToDto).ToList();
        }

        public async Task<InventoryDto> CreateAsync(InventoryUpsertDto dto, int userId)
        {
            _logger.LogInformation("Creating Inventory — ProductId: {ProductId}, Quantity: {Quantity}", dto.ProductId, dto.Quantity);

            Inventory entity = new Inventory
            {
                ProductId = dto.ProductId,
                ProductCategoryId = dto.ProductCategoryId,
                Quantity = dto.Quantity,
            };

            Inventory created = await _repository.CreateAsync(entity, userId);

            return MapToDto(created);
        }

        public async Task<InventoryDto?> UpdateAsync(InventoryUpsertDto dto, int userId)
        {
            _logger.LogInformation("Updating Inventory with Id: {Id}", dto.Id);

            Inventory entity = new Inventory
            {
                Id = dto.Id,
                ProductId = dto.ProductId,
                ProductCategoryId = dto.ProductCategoryId,
                Quantity = dto.Quantity,
            };

            Inventory? updated = await _repository.UpdateAsync(entity, userId);

            if (updated is null)
            {
                _logger.LogWarning("Inventory with Id: {Id} not found for update", dto.Id);
                return null;
            }

            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            _logger.LogInformation("Deleting Inventory with Id: {Id}", id);

            return await _repository.DeleteAsync(id, userId);
        }

        public async Task<List<InventoryDto>> GetByProductCategoryAsync(int productCategoryId, int pageNumber, int pageSize, bool track = false)
        {
            _logger.LogInformation(
                "Getting Inventory — ProductCategoryId: {ProductCategoryId}, PageNumber: {PageNumber}, PageSize: {PageSize}",
                productCategoryId, pageNumber, pageSize);

            List<Inventory> entities = await _repository.GetByProductCategoryAsync(productCategoryId, pageNumber, pageSize, track);

            return entities.Select(MapToDto).ToList();
        }

        private static InventoryDto MapToDto(Inventory entity)
        {
            return new InventoryDto
            {
                Id = entity.Id,
                ProductId = entity.ProductId,
                ProductCategoryId = entity.ProductCategoryId,
                Quantity = entity.Quantity,
                ReservedQuantity = entity.ReservedQuantity,
                AvailableQuantity = entity.Quantity - entity.ReservedQuantity,
                CreatedBy = entity.CreatedBy,
                CreatedDate = entity.CreatedDate,
                ModifiedBy = entity.ModifiedBy,
                ModifiedDate = entity.ModifiedDate,
                IsDeleted = entity.IsDeleted
            };
        }
    }
}
