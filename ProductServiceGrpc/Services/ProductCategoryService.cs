using ProductServiceGrpc.Models;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ProductServiceGrpc;
using ProductServiceGrpc.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProductServiceGrpc.Services
{
    public class ProductCategoryService : ProductCategory.ProductCategoryBase
    {
        private readonly IProductCategoryRepository _repo;

        public ProductCategoryService(IProductCategoryRepository repo)
        {
            _repo = repo;
        }

        public override async Task<ProductCategoryCreateResponse> CreateCategory(ProductCategoryCreateRequest request, ServerCallContext context)
        {
            try
            {
                var existing = await _repo.GetCategoryByNameAsync(request.Dto.CategoryName);
                if (existing != null)
                {
                    return new ProductCategoryCreateResponse
                    {
                        Status = -1,
                        ErrorMessage = "Product category already exists",
                        Dto = new ProductCategoryDto()
                    };
                }

                var newCategory = new ProductCategoryModel
                {
                    CategoryName = request.Dto.CategoryName,
                    CreatedBy = request.UserId,
                    CreatedDate = DateTime.UtcNow
                };

                ProductCategoryModel c = await _repo.CreateCategoryAsync(newCategory);

                if (c == null)
                {
                    return new ProductCategoryCreateResponse
                    {
                        Status = -1,
                        ErrorMessage = "Category create failed",
                        Dto = new ProductCategoryDto
                        {
                            Id = newCategory.Id,
                            CategoryName = newCategory.CategoryName
                        }
                    };
                }

                return new ProductCategoryCreateResponse
                {
                    Status = 1,
                    ErrorMessage = "Category created successfully",
                    Dto = new ProductCategoryDto
                    {
                        Id = newCategory.Id,
                        CategoryName = newCategory.CategoryName
                    }
                };
            }
            catch (Exception ex)
            {
                return new ProductCategoryCreateResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to create category: {ex.Message}",
                    Dto = new ProductCategoryDto()
                };
            }
        }

        public override async Task<ProductCategoryDto> GetCategoryById(ProductCategorySingleRequest request, ServerCallContext context)
        {
            try
            {
                var category = await _repo.GetCategoryByIdAsync(request.Id);
                if (category == null || category.IsDeleted)
                    throw new RpcException(new Status(StatusCode.NotFound, "Category not found"));

                return new ProductCategoryDto
                {
                    Id = category.Id,
                    CategoryName = category.CategoryName
                };
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error fetching category: {ex.Message}"));
            }
        }

        public override async Task<ProductCategoryMultipleResponse> GetAllCategories(Empty request, ServerCallContext context)
        {
            var response = new ProductCategoryMultipleResponse();
            try
            {
                var categories = await _repo.GetAllCategoriesAsync();

                response.Dtos.AddRange(categories
                    .Where(c => !c.IsDeleted)
                    .Select(c => new ProductCategoryDto
                    {
                        Id = c.Id,
                        CategoryName = c.CategoryName
                    }));
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, $"Error retrieving categories: {ex.Message}"));
            }

            return response;
        }

        public override async Task<ProductCategoryCreateResponse> UpdateCategory(ProductCategoryCreateRequest request, ServerCallContext context)
        {
            try
            {
                var existing = await _repo.GetCategoryByIdAsync(request.Dto.Id);
                if (existing == null || existing.IsDeleted)
                {
                    return new ProductCategoryCreateResponse
                    {
                        Status = -1,
                        ErrorMessage = "Category not found",
                        Dto = new ProductCategoryDto()
                    };
                }

                existing.CategoryName = request.Dto.CategoryName;
                existing.ModifiedBy = request.UserId;
                existing.ModifiedDate = DateTime.UtcNow;

                await _repo.UpdateCategoryAsync(existing);

                return new ProductCategoryCreateResponse
                {
                    Status = 1,
                    ErrorMessage = "Category updated successfully",
                    Dto = new ProductCategoryDto
                    {
                        Id = existing.Id,
                        CategoryName = existing.CategoryName
                    }
                };
            }
            catch (Exception ex)
            {
                return new ProductCategoryCreateResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to update category: {ex.Message}",
                    Dto = new ProductCategoryDto()
                };
            }
        }

        public override async Task<ProductCategoryCreateResponse> DeleteCategory(ProductCategoryDeleteRequest request, ServerCallContext context)
        {
            try
            {
                var category = await _repo.GetCategoryByIdAsync(request.Id);
                if (category == null || category.IsDeleted)
                {
                    return new ProductCategoryCreateResponse
                    {
                        Status = -1,
                        ErrorMessage = "Category not found or already deleted",
                        Dto = new ProductCategoryDto()
                    };
                }

                category.IsDeleted = true;
                category.ModifiedBy = request.Userid;
                category.ModifiedDate = DateTime.UtcNow;

                await _repo.UpdateCategoryAsync(category); // Use soft delete

                return new ProductCategoryCreateResponse
                {
                    Status = 1,
                    ErrorMessage = "Category deleted successfully",
                    Dto = new ProductCategoryDto
                    {
                        Id = category.Id,
                        CategoryName = category.CategoryName
                    }
                };
            }
            catch (Exception ex)
            {
                return new ProductCategoryCreateResponse
                {
                    Status = -1,
                    ErrorMessage = $"Failed to delete category: {ex.Message}",
                    Dto = new ProductCategoryDto()
                };
            }
        }
    }
}
