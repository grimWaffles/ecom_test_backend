using ProductServiceGrpc.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ProductServiceGrpc;
using ProductServiceGrpc.Repository;
using System.Threading.Tasks;

namespace ProductServiceGrpc.Services
{
    public class ProductService : ProductServiceGrpc.ProductService.ProductServiceBase
    {
        private readonly IProductRepository _repo;

        public ProductService(IProductRepository repo)
        {
            _repo = repo;
        }

        public override Task<ProductServiceTestMessage> TestProductService(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new ProductServiceTestMessage()
            {
                StatusMessage = "Product service up and running."
            });
        }

        public override async Task<ProductResponse> CreateProduct(ProductRequest request, ServerCallContext context)
        {
            var response = new ProductResponse();
            try
            {
                var product = new ProductModel
                {
                    Name = request.Dto.Name,
                    DefaultQuantity = request.Dto.DefaultQuantity,
                    Rating = (decimal)request.Dto.Rating,
                    Price = (decimal)request.Dto.Price,
                    Description = request.Dto.Description,
                    SellerId = request.Dto.SellerId,
                    ProductCategoryId = request.Dto.ProductCategoryId,
                    CreatedBy = request.UserId
                };

                var result = await _repo.CreateProductAsync(product);

                if (result == null)
                    throw new Exception("Could not save product.");

                response.Status = 1;
                response.ErrorMessage = "Product created successfully";
                response.Dto = MapToDto(result);
            }
            catch (Exception ex)
            {
                response.Status = -1;
                response.ErrorMessage = $"Create failed: {ex.Message}";
                response.Dto = new ProductDto();
            }

            return response;
        }

        public override async Task<ProductDto> GetProductById(ProductIdRequest request, ServerCallContext context)
        {
            try
            {
                var product = await _repo.GetProductByIdAsync(request.Id);
                return product != null ? MapToDto(product) : new ProductDto();
            }
            catch
            {
                return new ProductDto();
            }
        }

        public override async Task<ProductListResponse> GetAllProducts(Google.Protobuf.WellKnownTypes.Empty request, ServerCallContext context)
        {
            var response = new ProductListResponse();
            try
            {
                Tuple<string,List<ProductModel>> products = await _repo.GetAllProductsAsync();
                if (products.Item2 != null)
                {
                    response.Dtos.AddRange(products.Item2.Select(MapToDto));
                    response.Status = 1;
                    response.ErrorMessage = $"Found {products.Item2.Count} products.";
                }
                else
                {
                    response.Status = -1;
                    response.ErrorMessage = $"{products.Item1}";
                }
            }
            catch(Exception e)
            {
                response.Status = -1;
                response.ErrorMessage = e.Message;
                // Return empty list on failure
            }

            return response;
        }

        public override async Task<ProductResponse> UpdateProduct(ProductRequest request, ServerCallContext context)
        {
            var response = new ProductResponse();
            try
            {
                var updatedProduct = new ProductModel
                {
                    Id = request.Dto.Id,
                    Name = request.Dto.Name,
                    DefaultQuantity = request.Dto.DefaultQuantity,
                    Rating = (decimal)request.Dto.Rating,
                    Price = (decimal)request.Dto.Price,
                    Description = request.Dto.Description,
                    SellerId = request.Dto.SellerId,
                    ProductCategoryId = request.Dto.ProductCategoryId,
                    ModifiedBy = request.UserId,
                };

                var result = await _repo.UpdateProductAsync(updatedProduct);

                response.Status = result ? 1 : -1;
                response.ErrorMessage = result ? "Product updated" : "Update failed";
                response.Dto = request.Dto;
            }
            catch (Exception ex)
            {
                response.Status = -1;
                response.ErrorMessage = $"Update failed: {ex.Message}";
                response.Dto = new ProductDto();
            }

            return response;
        }

        public override async Task<ProductResponse> DeleteProduct(ProductDeleteRequest request, ServerCallContext context)
        {
            var response = new ProductResponse();
            try
            {
                var result = await _repo.DeleteProductAsync(request.Id, request.ModifiedBy);

                response.Status = result ? 1 : -1;
                response.ErrorMessage = result ? "Deleted successfully" : "Delete failed";
                response.Dto = new ProductDto { Id = request.Id };
            }
            catch (Exception ex)
            {
                response.Status = -1;
                response.ErrorMessage = $"Delete failed: {ex.Message}";
                response.Dto = new ProductDto();
            }

            return response;
        }

        private ProductDto MapToDto(ProductModel model)
        {
            return new ProductDto
            {
                Id = model.Id,
                Name = model.Name,
                DefaultQuantity = model.DefaultQuantity ?? 0,
                Rating = (double)model.Rating,
                Price = (double)model.Price,
                Description = model.Description ?? "",
                SellerId = model.SellerId,
                ProductCategoryId = model.ProductCategoryId,
                SellerCompanyName = model.Seller?.CompanyName ?? "",
                ProductCategoryName = model.ProductCategory?.CategoryName ?? ""
            };
        }
    }
}