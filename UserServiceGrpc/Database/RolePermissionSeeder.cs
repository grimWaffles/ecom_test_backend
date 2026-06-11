using Google.Protobuf.Collections;
using Microsoft.AspNetCore.Http.HttpResults;
using Mysqlx.Crud;
using System;
using System.Data;
using System.Security.Principal;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Database;

public class RolePermissionSeeder
{
    private readonly AppDbContext _context;
    public string[] EntitiesArray { get; set; }

    public RolePermissionSeeder(AppDbContext dbContext)
    {
        _context = dbContext;
        EntitiesArray = ["cart", "order", "productCategory", "product", "seller", "user"];
    }

    public void SeedRolePermissions()
    {
        Console.WriteLine("Seeding in progress");

//        SELECT* FROM ECommercePlatform.dbo.SecurityPermissions

//drop table if exists RolePermissions
//create table RolePermissions(
//    Id bigint identity(1,1) primary key,
//    RoleId int not null

//        foreign key references Roles(Id),
//	PermissionId bigint not null

//        foreign key references SecurityPermissions(Id),

//	CreatedBy int NOT NULL
//        foreign key references Users(Id),
//    CreatedDate datetime NOT NULL,

//	ModifiedBy int NULL

//        foreign key references Users(Id),
//	ModifiedDate datetime NULL,

//	IsDelete bit NOT NULL default 0,
//)

//alter table RolePermissions add constraint UQ_Role_Permission UNIQUE(RoleId, PermissionId)

//select* from RolePermissions
        //if (_context.RolePermissions.Any())
        //{
        //    return;
        //}
        //else
        //{
        //    List<Role> roleCount = _context.Roles.Where(x => !x.IsDeleted).ToList();
        //    Console.WriteLine($"Total roles in the system is {roleCount.Count()}");

        //    if (roleCount.Count() == 0) { return; }

        //    List<RolePermissions> listToAdd = new List<RolePermissions>();

        //    foreach (Role r in roleCount)
        //    {
        //        foreach (string entity in EntitiesArray)
        //        {
        //            listToAdd.Add(new RolePermissions()
        //            {
        //                RoleId = r.Id,
        //                ApiPath = "/api/" + entity+"/",
        //                ViewPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
        //                AddPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
        //                EditPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
        //                DeletePermission = r.Name.ToString().ToLower() == "admin" ? true : false,
        //                CreatedBy = 1,
        //                CreatedDate = DateTime.UtcNow,
        //                ModifiedBy = null,
        //                ModifiedDate = null
        //            });
        //        }
        //    }
        //    Console.WriteLine($"Total rows to add {listToAdd.Count()}");

        //    _context.AddRange(listToAdd);
        //    _context.SaveChanges();
        //}

        Console.WriteLine("Seeding complete");
    }
}
