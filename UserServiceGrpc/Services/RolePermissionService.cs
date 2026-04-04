using System;
using UserServiceGrpc.Models.Dtos;

using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services;

// Services/Interfaces/IRolePermissionsService.cs
public interface IRolePermissionsService
{
    Task<RolePermissionsResponseDto?> GetByIdAsync(int id);
    Task<IEnumerable<RolePermissionsResponseDto>> GetAllAsync();
    Task<IEnumerable<RolePermissionsResponseDto>> GetByRoleIdAsync(int roleId);
    Task<RolePermissionsResponseDto?> GetByRoleIdAndPathAsync(int roleId, string apiPath);
    Task<RolePermissionsResponseDto> CreateAsync(CreateRolePermissionsDto dto);
    Task<RolePermissionsResponseDto?> UpdateAsync(UpdateRolePermissionsDto dto);
    Task<bool> DeleteAsync(int id);
}
// Services/RolePermissionsService.cs
public class RolePermissionsService : IRolePermissionsService
{
    private readonly IRolePermissionsRepository _repository;
    private readonly ILogger<RolePermissionsService> _logger;

    public RolePermissionsService(
        IRolePermissionsRepository repository,
        ILogger<RolePermissionsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RolePermissionsResponseDto?> GetByIdAsync(int id)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(id);

            if (entity is null)
            {
                _logger.LogWarning("RolePermission with Id {Id} was not found", id);
                return null;
            }

            return MapToResponseDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetByIdAsync for Id {Id}", id);
            throw;
        }
    }

    public async Task<IEnumerable<RolePermissionsResponseDto>> GetAllAsync()
    {
        try
        {
            var entities = await _repository.GetAllAsync();
            return entities.Select(MapToResponseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAllAsync");
            throw;
        }
    }

    public async Task<IEnumerable<RolePermissionsResponseDto>> GetByRoleIdAsync(int roleId)
    {
        try
        {
            var entities = await _repository.GetByRoleIdAsync(roleId);
            return entities.Select(MapToResponseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetByRoleIdAsync for RoleId {RoleId}", roleId);
            throw;
        }
    }

    public async Task<RolePermissionsResponseDto?> GetByRoleIdAndPathAsync(int roleId, string apiPath)
    {
        try
        {
            var entity = await _repository.GetByRoleIdAndPathAsync(roleId, apiPath);

            if (entity is null)
            {
                _logger.LogWarning(
                    "RolePermission not found for RoleId {RoleId} and ApiPath {ApiPath}",
                    roleId, apiPath);
                return null;
            }

            return MapToResponseDto(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in GetByRoleIdAndPathAsync for RoleId {RoleId} and ApiPath {ApiPath}",
                roleId, apiPath);
            throw;
        }
    }

    public async Task<RolePermissionsResponseDto> CreateAsync(CreateRolePermissionsDto dto)
    {
        try
        {
            // Guard against duplicate role + path combinations
            var exists = await _repository.ExistsAsync(dto.RoleId, dto.ApiPath);

            if (exists)
            {
                _logger.LogWarning(
                    "Duplicate RolePermission for RoleId {RoleId} and ApiPath {ApiPath}",
                    dto.RoleId, dto.ApiPath);
                throw new InvalidOperationException(
                    $"A permission entry for RoleId {dto.RoleId} and path '{dto.ApiPath}' already exists.");
            }

            var entity = new RolePermissions
            {
                RoleId          = dto.RoleId,
                ApiPath         = dto.ApiPath,
                ViewPermission  = dto.ViewPermission,
                AddPermission   = dto.AddPermission,
                EditPermission  = dto.EditPermission,
                DeletePermission = dto.DeletePermission,
                CreatedBy       = dto.CreatedBy,
                CreatedDate     = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(entity);
            return MapToResponseDto(created);
        }
        catch (InvalidOperationException)
        {
            throw; // Let the controller handle this as a 409 Conflict
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateAsync for RoleId {RoleId}", dto.RoleId);
            throw;
        }
    }

    public async Task<RolePermissionsResponseDto?> UpdateAsync(UpdateRolePermissionsDto dto)
    {
        try
        {
            var entity = await _repository.GetByIdAsync(dto.Id);

            if (entity is null)
            {
                _logger.LogWarning("RolePermission with Id {Id} not found for update", dto.Id);
                return null;
            }

            // Only update editable fields — audit and role fields are untouched
            entity.ApiPath          = dto.ApiPath;
            entity.ViewPermission   = dto.ViewPermission;
            entity.AddPermission    = dto.AddPermission;
            entity.EditPermission   = dto.EditPermission;
            entity.DeletePermission = dto.DeletePermission;
            entity.ModifiedBy       = dto.ModifiedBy;
            entity.ModifiedDate     = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(entity);
            return MapToResponseDto(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateAsync for Id {Id}", dto.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var deleted = await _repository.DeleteAsync(id);

            if (!deleted)
                _logger.LogWarning("RolePermission with Id {Id} not found for deletion", id);

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteAsync for Id {Id}", id);
            throw;
        }
    }

    // Private mapping method — single place to maintain DTO shape
    private static RolePermissionsResponseDto MapToResponseDto(RolePermissions entity)
    {
        return new RolePermissionsResponseDto
        {
            Id              = entity.Id,
            RoleId          = entity.RoleId,
            RoleName        = "",
            ApiPath         = entity.ApiPath,
            ViewPermission  = entity.ViewPermission,
            AddPermission   = entity.AddPermission,
            EditPermission  = entity.EditPermission,
            DeletePermission = entity.DeletePermission,
            CreatedBy       = entity.CreatedBy,
            ModifiedBy      = entity.ModifiedBy,
            CreatedDate     = entity.CreatedDate,
            ModifiedDate    = entity.ModifiedDate
        };
    }
}
