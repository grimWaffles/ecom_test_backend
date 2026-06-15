using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Helpers
{
    public static class Mapper
    {
        public static RolePermissionDto CreateRolePermissionDtoFromModel(RolePermission model)
        {
            return new RolePermissionDto()
            {
                Id = model.Id,
                RoleId = model.RoleId,
                PermissionId = model.PermissionId,
                RoleName = model.Role?.Name ?? "",
                PermissionName = model.Permission?.Permission ?? ""
            };
        }

        public static RolePermission CreateRolePermissionModelFromDto(RolePermissionDto dto)
        {
            return new RolePermission()
            {
                Id = dto.Id,
                RoleId = dto.RoleId,
                PermissionId = dto.PermissionId
            };
        }
    }
}
