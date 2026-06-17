using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Models.Dtos
{
    public class RolePermissionDto
    {
        public long Id { get; set; }
        public int RoleId { get; set; }
        public long PermissionId { get; set; }
        public string RoleName { get; set; }
        public string PermissionName { get; set; }
    }
}
