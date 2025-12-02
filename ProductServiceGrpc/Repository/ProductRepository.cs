using ProductServiceGrpc.Models;
using Microsoft.EntityFrameworkCore;
using ProductServiceGrpc.Database;

namespace ProductServiceGrpc.Repository
{
    public interface IProductRepository
    {
        Task<ProductModel> CreateProductAsync(ProductModel productModel);
        Task<ProductModel> GetProductByIdAsync(int id);
        Task<Tuple<string, List<ProductModel>>> GetAllProductsAsync();
        Task<bool> UpdateProductAsync(ProductModel updatedProduct);
        Task<bool> DeleteProductAsync(int id, int modifiedBy);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _db;

        public ProductRepository(AppDbContext db)
        {
            _db = db;
        }

        // CREATE
        public async Task<ProductModel> CreateProductAsync(ProductModel productModel)
        {
            productModel.CreatedDate = DateTime.UtcNow;
            try
            {
                _db.Products.Add(productModel);
                await _db.SaveChangesAsync();
                return productModel;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        // READ (Get by Id)
        public async Task<ProductModel> GetProductByIdAsync(int id)
        {
            try
            {
                return await _db.Products
                            .Include(p => p.Seller)
                            .Include(p => p.ProductCategory)
                            .FirstAsync(p => p.Id == id && !p.IsDeleted);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        // READ (Get all)
        public async Task<Tuple<string, List<ProductModel>>> GetAllProductsAsync()
        {
            List<ProductModel> products = new List<ProductModel>();
            string response = "";
            try
            {
                products = await _db.Products
                            .Where(p => !p.IsDeleted)
                            .Include(p => p.Seller)
                            .Include(p => p.ProductCategory)
                            .ToListAsync();

                response = products == null ? "No products found" : $"Fetched {products.Count} rows";

                return Tuple.Create(response, products);
            }
            catch (Exception e)
            {
                products = new List<ProductModel>();
                response = $"Error: {e.Message}";

                return Tuple.Create(response,products);
            }
        }

        // UPDATE
        public async Task<bool> UpdateProductAsync(ProductModel updatedProduct)
        {
            try
            {
                var existingProduct = await _db.Products.FindAsync(updatedProduct.Id);
                if (existingProduct == null || existingProduct.IsDeleted)
                    return false;

                existingProduct.Name = updatedProduct.Name;
                existingProduct.DefaultQuantity = updatedProduct.DefaultQuantity;
                existingProduct.Rating = updatedProduct.Rating;
                existingProduct.Price = updatedProduct.Price;
                existingProduct.Description = updatedProduct.Description;
                existingProduct.SellerId = updatedProduct.SellerId;
                existingProduct.ProductCategoryId = updatedProduct.ProductCategoryId;
                existingProduct.ModifiedBy = updatedProduct.ModifiedBy;
                existingProduct.ModifiedDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        // DELETE (Soft Delete)
        public async Task<bool> DeleteProductAsync(int id, int modifiedBy)
        {
            try
            {
                var productModel = await _db.Products.FindAsync(id);
                if (productModel == null || productModel.IsDeleted)
                    return false;

                productModel.IsDeleted = true;
                productModel.ModifiedDate = DateTime.UtcNow;
                productModel.ModifiedBy = modifiedBy;

                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

}
