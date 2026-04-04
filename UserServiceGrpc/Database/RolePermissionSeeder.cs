using System;
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
    
        if (_context.RolePermissions.Any())
        {
            return;
        }
        else
        {
            List<Role> roleCount = _context.Roles.Where(x => !x.IsDeleted).ToList();
            Console.WriteLine($"Total roles in the system is {roleCount.Count()}");

            if (roleCount.Count() == 0) { return; }

            List<RolePermissions> listToAdd = new List<RolePermissions>();

            foreach (Role r in roleCount)
            {
                foreach (string entity in EntitiesArray)
                {
                    listToAdd.Add(new RolePermissions()
                    {
                        RoleId = r.Id,
                        ApiPath = entity,
                        ViewPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
                        AddPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
                        EditPermission = r.Name.ToString().ToLower() == "admin" ? true : false,
                        DeletePermission = r.Name.ToString().ToLower() == "admin" ? true : false,
                        CreatedBy = 1,
                        CreatedDate = DateTime.UtcNow,
                        ModifiedBy = null,
                        ModifiedDate = null
                    });
                }
            }
            Console.WriteLine($"Total rows to add {listToAdd.Count()}");

            _context.AddRange(listToAdd);
            _context.SaveChanges();
        }

        Console.WriteLine("Seeding complete");
    }
}
