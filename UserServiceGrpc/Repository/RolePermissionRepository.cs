using System;
using Microsoft.EntityFrameworkCore;
using UserServiceGrpc.Database;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Repository;

public interface IRolePermissionsRepository
{
    Task<RolePermissions?> GetByIdAsync(int id);
    Task<IEnumerable<RolePermissions>> GetAllAsync();
    Task<IEnumerable<RolePermissions>> GetByRoleIdAsync(int roleId);
    Task<RolePermissions?> GetByRoleIdAndPathAsync(int roleId, string apiPath);
    Task<RolePermissions> CreateAsync(RolePermissions entity);
    Task<RolePermissions> UpdateAsync(RolePermissions entity);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int roleId, string apiPath);
}

// Repositories/RolePermissionsRepository.cs
public class RolePermissionsRepository : IRolePermissionsRepository
{
    private readonly AppDbContext _db;
    private readonly ILogger<RolePermissionsRepository> _logger;

    public RolePermissionsRepository(AppDbContext db, ILogger<RolePermissionsRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RolePermissions?> GetByIdAsync(int id)
    {
        try
        {
            return await _db.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.CreatedByUser)
                .Include(rp => rp.ModifiedByUser)
                .FirstOrDefaultAsync(rp => rp.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching RolePermission with Id {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<RolePermissions>> GetAllAsync()
    {
        try
        {
            return await _db.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.CreatedByUser)
                .Include(rp => rp.ModifiedByUser)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all RolePermissions");
            throw;
        }
    }

    public async Task<IEnumerable<RolePermissions>> GetByRoleIdAsync(int roleId)
    {
        try
        {
            return await _db.RolePermissions
                .Include(rp => rp.Role)
                .Where(rp => rp.RoleId == roleId)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching RolePermissions for RoleId {RoleId}", roleId);
            throw;
        }
    }

    public async Task<RolePermissions?> GetByRoleIdAndPathAsync(int roleId, string apiPath)
    {
        try
        {
            return await _db.RolePermissions
                .AsNoTracking()
                .FirstOrDefaultAsync(rp =>
                    rp.RoleId == roleId &&
                    rp.ApiPath == apiPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error fetching RolePermission for RoleId {RoleId} and ApiPath {ApiPath}",
                roleId, apiPath);
            throw;
        }
    }

    public async Task<RolePermissions> CreateAsync(RolePermissions entity)
    {
        try
        {
            await _db.RolePermissions.AddAsync(entity);
            await _db.SaveChangesAsync();
            return entity;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Database error creating RolePermission for RoleId {RoleId}", entity.RoleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error creating RolePermission for RoleId {RoleId}", entity.RoleId);
            throw;
        }
    }

    public async Task<RolePermissions> UpdateAsync(RolePermissions entity)
    {
        try
        {
            _db.RolePermissions.Update(entity);
            await _db.SaveChangesAsync();
            return entity;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex,
                "Concurrency error updating RolePermission with Id {Id}", entity.Id);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Database error updating RolePermission with Id {Id}", entity.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error updating RolePermission with Id {Id}", entity.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var entity = await _db.RolePermissions.FindAsync(id);

            if (entity is null)
                return false;

            _db.RolePermissions.Remove(entity);
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting RolePermission with Id {Id}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting RolePermission with Id {Id}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int roleId, string apiPath)
    {
        try
        {
            return await _db.RolePermissions.AnyAsync(rp =>
                rp.RoleId == roleId &&
                rp.ApiPath == apiPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking existence for RoleId {RoleId} and ApiPath {ApiPath}",
                roleId, apiPath);
            throw;
        }
    }
}
