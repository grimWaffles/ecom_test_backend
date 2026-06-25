using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Http.HttpResults;
using Mysqlx.Crud;
using System;
using System.Data;
using System.Security.Principal;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Database;

public class RolePermissionSeeder
{
    private readonly AppDbContext _context;
    public string[] EntitiesArray { get; set; }
    public string[] ActionArray { get; set; }

    public RolePermissionSeeder(AppDbContext dbContext)
    {
        _context = dbContext;
        EntitiesArray = ["cart", "order", "productcategory", "product", "seller", "user", "permission", "role","securitypermission"];
        ActionArray = ["create", "view", "update", "delete"];
    }

    public void SeedRolePermissions()
    {
        Console.WriteLine("Seeding in progress");

        //Setup the permission table if not exists
        if (!_context.SecurityPermissions.Any())
        {
            List<SecurityPermission> permissionsToAdd = new List<SecurityPermission>();

            try
            {
                foreach (string entity in EntitiesArray)
                {
                    foreach (string action in ActionArray)
                    {
                        permissionsToAdd.Add(new SecurityPermission()
                        {
                            Permission = entity + '.' + action,
                            CreatedBy = 1,
                            CreatedDate = DateTime.Now,
                            IsDeleted = false
                        });
                    }
                }

                _context.SecurityPermissions.AddRange(permissionsToAdd);
                _context.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to add Security Permissions");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }

        //Setup role-wise permissions
        if (!_context.RolePermissions.Any())
        {
            try
            {
                List<RolePermission> rpsToAdd = new List<RolePermission>();

                List<SecurityPermissionDto> permissionDtos = _context.SecurityPermissions
                    .Where(x => !x.IsDeleted)
                    .Select(x => new SecurityPermissionDto()
                    {
                        Id = x.Id,
                        PermissionName = x.Permission
                    }).ToList();

                List<Role> roles = _context.Roles.ToList();

                //For Admin (All permissions)
                int adminRoleId = roles.Where(x => x.Name.ToLower() == "admin").Select(x => x.Id).First();

                foreach (SecurityPermissionDto dto in permissionDtos)
                {
                    rpsToAdd.Add(new RolePermission()
                    {
                        RoleId = adminRoleId,
                        PermissionId = dto.Id,
                        CreatedBy = 1,
                        CreatedDate = DateTime.Now,
                        IsDeleted = false
                    });
                }

                //For Seller
                int sellerRoleId = roles.Where(x => x.Name.ToLower() == "seller").Select(x => x.Id).First();

                foreach (SecurityPermissionDto dto in permissionDtos
                    .Where(x =>
                        x.PermissionName.Contains("seller") ||
                        x.PermissionName.Contains("product") ||
                        x.PermissionName.Contains("productcategory"))
                    )
                {
                    rpsToAdd.Add(new RolePermission()
                    {
                        RoleId = sellerRoleId,
                        PermissionId = dto.Id,
                        CreatedBy = 1,
                        CreatedDate = DateTime.Now,
                        IsDeleted = false
                    });
                }

                //For Customer
                int customerRoleId = roles.Where(x => x.Name.ToLower() == "customer").Select(x => x.Id).First();

                foreach (SecurityPermissionDto dto in permissionDtos
                    .Where(x =>
                        x.PermissionName.Contains("cart") ||
                        x.PermissionName.Contains("order") ||
                        x.PermissionName.Contains("product.view"))
                    )
                {
                    rpsToAdd.Add(new RolePermission()
                    {
                        RoleId = customerRoleId,
                        PermissionId = dto.Id,
                        CreatedBy = 1,
                        CreatedDate = DateTime.Now,
                        IsDeleted = false
                    });
                }

                _context.RolePermissions.AddRange(rpsToAdd);
                _context.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to add Role Permissions");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw;
            }
        }

        Console.WriteLine("Seeding complete");
    }
}
