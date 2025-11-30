using ProductServiceGrpc.Models;
using Microsoft.EntityFrameworkCore;
using ProductServiceGrpc.Database;

namespace ProductServiceGrpc.Repository
{
    public interface ISellerRepository
    {
        Task<SellerModel> CreateSellerAsync(SellerModel seller);
        Task<SellerModel> GetSellerByIdAsync(int id);
        Task<List<SellerModel>> GetAllSellersAsync();
        Task<bool> UpdateSellerAsync(SellerModel updatedSeller);
        Task<bool> DeleteSellerAsync(int id, int modifiedBy);
    }
    public class SellerRepository : ISellerRepository
    {
        private readonly AppDbContext _db;

        public SellerRepository(AppDbContext db)
        {
            _db = db;
        }

        // CREATE
        public async Task<SellerModel> CreateSellerAsync(SellerModel seller)
        {
            seller.CreatedDate = DateTime.UtcNow;
            _db.Sellers.Add(seller);
            await _db.SaveChangesAsync();
            return seller;
        }

        // READ (Get by Id)
        public async Task<SellerModel> GetSellerByIdAsync(int id)
        {
            return await _db.Sellers.AsNoTracking()
                            .Include(s => s.Products)
                            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
        }

        // READ (Get all)
        public async Task<List<SellerModel>> GetAllSellersAsync()
        {
            return await _db.Sellers
                            .AsNoTracking()
                            .Where(s => !s.IsDeleted)
                            .Include(s => s.Products)
                            .ToListAsync();
        }

        // UPDATE
        public async Task<bool> UpdateSellerAsync(SellerModel updatedSeller)
        {
            var existingSeller = await _db.Sellers.FindAsync(updatedSeller.Id);
            if (existingSeller == null || existingSeller.IsDeleted)
                return false;

            existingSeller.CompanyName = updatedSeller.CompanyName;
            existingSeller.Address = updatedSeller.Address;
            existingSeller.MobileNo = updatedSeller.MobileNo;
            existingSeller.Email = updatedSeller.Email;
            existingSeller.Rating = updatedSeller.Rating;
            existingSeller.ModifiedBy = updatedSeller.ModifiedBy;
            existingSeller.ModifiedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        // DELETE (Soft Delete)
        public async Task<bool> DeleteSellerAsync(int id, int modifiedBy)
        {
            var seller = await _db.Sellers.FindAsync(id);
            if (seller == null || seller.IsDeleted)
                return false;

            seller.IsDeleted = true;
            seller.ModifiedBy = modifiedBy;
            seller.ModifiedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }
    }

}
