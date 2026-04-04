using System;
using System.ComponentModel.DataAnnotations;

namespace UserServiceGrpc.Models.Dtos;

// DTOs/RolePermissions/RolePermissionsResponseDto.cs
public class RolePermissionsResponseDto
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; }
    public string ApiPath { get; set; }
    public bool ViewPermission { get; set; }
    public bool AddPermission { get; set; }
    public bool EditPermission { get; set; }
    public bool DeletePermission { get; set; }
    public int CreatedBy { get; set; }
    public int? ModifiedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

// DTOs/RolePermissions/CreateRolePermissionsDto.cs
public class CreateRolePermissionsDto
{
    [Required]
    public int RoleId { get; set; }

    [Required]
    [MaxLength(255)]
    public string ApiPath { get; set; }

    public bool ViewPermission { get; set; } = false;
    public bool AddPermission { get; set; } = false;
    public bool EditPermission { get; set; } = false;
    public bool DeletePermission { get; set; } = false;

    [Required]
    public int CreatedBy { get; set; }
}

// DTOs/RolePermissions/UpdateRolePermissionsDto.cs
public class UpdateRolePermissionsDto
{
    [Required]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string ApiPath { get; set; }

    public bool ViewPermission { get; set; }
    public bool AddPermission { get; set; }
    public bool EditPermission { get; set; }
    public bool DeletePermission { get; set; }

    [Required]
    public int ModifiedBy { get; set; }
}
