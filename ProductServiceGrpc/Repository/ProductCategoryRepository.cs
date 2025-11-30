using ProductServiceGrpc.Models;
using Microsoft.EntityFrameworkCore;
using ProductServiceGrpc.Database;

namespace ProductServiceGrpc.Repository
{
    public interface IProductCategoryRepository
    {
        Task<ProductCategoryModel> CreateCategoryAsync(ProductCategoryModel category);
        Task<ProductCategoryModel> GetCategoryByIdAsync(int id);
        Task<ProductCategoryModel> GetCategoryByNameAsync(string name);

        Task<List<ProductCategoryModel>> GetAllCategoriesAsync();
        Task<bool> UpdateCategoryAsync(ProductCategoryModel updatedCategory);
    }

    public class ProductCategoryRepository : IProductCategoryRepository
    {
        private readonly AppDbContext _db;

        public ProductCategoryRepository(AppDbContext db)
        {
            _db = db;
        }

        // CREATE
        public async Task<ProductCategoryModel> CreateCategoryAsync(ProductCategoryModel category)
        {
            try
            {
                _db.ProductCategories.Add(category);
                await _db.SaveChangesAsync();
                return category;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        // READ (Get by Id)
        public async Task<ProductCategoryModel> GetCategoryByIdAsync(int id)
        {
            try
            {
                return await _db.ProductCategories
                            .Include(c => c.Products)
                            .FirstAsync(c => c.Id == id && !c.IsDeleted);
            }
            catch(Exception e)
            {
                return null;
            }
        }

        public async Task<ProductCategoryModel> GetCategoryByNameAsync(string name)
        {
            try
            {
                return await _db.ProductCategories.AsNoTracking().Where(pc => pc.CategoryName == name).FirstAsync();
            }
            catch (Exception e)
            {
                return null;
            }
        }

        // READ (Get all)
        public async Task<List<ProductCategoryModel>> GetAllCategoriesAsync()
        {
            return await _db.ProductCategories
                            .Where(c => !c.IsDeleted)
                            .Include(c => c.Products)
                            .ToListAsync();
        }

        // UPDATE
        public async Task<bool> UpdateCategoryAsync(ProductCategoryModel updatedCategory)
        {
            try
            {
                _db.ProductCategories.Update(updatedCategory);
                await _db.SaveChangesAsync();
                return true;
            }
            catch(Exception e)
            {
                return false;
            }
        }
    }

}
