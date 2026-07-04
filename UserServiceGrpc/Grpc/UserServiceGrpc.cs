using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using UserServiceGrpc.Authorization;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Services;

namespace UserServiceGrpc.Grpc
{
    [Authorize]
    public class UserGrpcService : User.UserBase
    {
        private readonly IUserService _service;
        private readonly ILogger<UserGrpcService> _logger;
        private readonly IRolePermissionService _rolePermissionService;

        public UserGrpcService(IUserService userService, ILogger<UserGrpcService> logger, IRolePermissionService rolePermissionService)
        {
            _service = userService;
            _logger = logger;
            _rolePermissionService = rolePermissionService;
        }

        //Test Functions
        [AllowAnonymous]
        public override async Task<TestResponse> TestService(Empty request, ServerCallContext context)
        {
            TestResponse response = new TestResponse();
            response.ServiceStatus = "User service is running.";

            string accessScope = "";
            response.ServiceStatus = accessScope != null && accessScope != "" ? accessScope : response.ServiceStatus;

            return await Task.FromResult(response);
        }

        // ── CRUD Operations ───────────────────────────────────────────────────────────
        [RequiresPermission("user.create")]
        public override async Task<UserCrudResponse> CreateUser(CreateUserRequest request, ServerCallContext context)
        {
            UserModel requestModel = ConvertRequestToModel(request);
            requestModel.CreatedBy = requestModel.RoleId == 1 ? 1 : request.UserId;
            requestModel.CreatedDate = DateTime.Now;

            ServiceResult result = await _service.CreateUser(requestModel);

            return new UserCrudResponse
            {
                Status = result.Status,
                ErrorMesage = result.Message
            };
        }

        [RequiresPermission("user.update")]
        public override async Task<UserCrudResponse> UpdateUser(CreateUserRequest request, ServerCallContext context)
        {
            UserModel requestModel = ConvertRequestToModel(request);
            requestModel.ModifiedBy = request.UserId;
            requestModel.ModifiedDate = DateTime.Now;

            ServiceResult result = await _service.UpdateUser(requestModel);

            return new UserCrudResponse
            {
                Status = result.Status,
                ErrorMesage = result.Message
            };
        }

        [RequiresPermission("user.delete")]
        public override async Task<UserCrudResponse> DeleteUser(UserRequestSingle request, ServerCallContext context)
        {
            ServiceResult result = await _service.DeleteUser(request.Id, request.UserId);

            return new UserCrudResponse
            {
                Status = result.Status,
                ErrorMesage = result.Message
            };
        }

        [RequiresPermission("user.view")]
        public override async Task<CreateUserRequest> GetUserByIdAsync(UserRequestSingle request, ServerCallContext context)
        {
            UserModel user = await _service.GetUserById(request.Id);

            return user == null ? null : ConvertModelToRequest(user);
        }

        [RequiresPermission("user.view")]
        public override async Task<UserResponseMultiple> GetAllUsers(Empty request, ServerCallContext context)
        {
            List<UserModel> users = await _service.GetUsers();

            if (users == null || users.Count == 0)
                return new UserResponseMultiple();

            UserResponseMultiple response = new UserResponseMultiple();
            response.Users.AddRange(users.Select(u => ConvertModelToRequest(u)));
            return response;
        }

        [RequiresPermission("user.view")]
        public override async Task GetAllUsersStream(Empty request, IServerStreamWriter<CreateUserRequest> responseStream, ServerCallContext context)
        {
            try
            {
                List<UserModel> users = await _service.GetUsers();

                foreach (UserModel user in users)
                    await responseStream.WriteAsync(ConvertModelToRequest(user));
            }
            catch (Exception)
            {
                await responseStream.WriteAsync(new CreateUserRequest { Id = 0 });
            }
        }

        // ── Authentication (unchanged per spec) ──────────────────────────────────────
        [AllowAnonymous]
        public override async Task<UserLoginResponse> LoginUser(UserLoginRequest request, ServerCallContext context)
        {
            LoginResponseDto response = await _service.LoginUser(request.Username, request.Password);

            return ToGrpcResponse(response);
        }

        [AllowAnonymous]
        public override async Task<UserLoginResponse> LogoutUser(UserRequestSingle request, ServerCallContext context)
        {
            return new UserLoginResponse
            {
                UserId = request.UserId,
                ErrorMessage = "Logout successful"
            };
        }

        //Role Permissions
        [RequiresPermission("permission.view")]
        public override async Task<GetAllRolePermissionsByRoleIdResponse> GetAllPermissionsByRoleId(GetAllRolePermissionsByRoleIdRequest request, ServerCallContext context)
        {
            try
            {
                List<RolePermissionDto> list = await _rolePermissionService.GetAllPermissionsByRoleId(request.RoleId);

                GetAllRolePermissionsByRoleIdResponse response = new GetAllRolePermissionsByRoleIdResponse();
                response.RolePermissions.AddRange(list.Select(x => new RolePermissionDto
                {
                    Id = x.Id,
                    RoleId = x.RoleId,
                    PermissionId = x.PermissionId,
                    RoleName = x.RoleName,
                    PermissionName = x.PermissionName
                }));

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

        [RequiresPermission("permission.view")]
        public override async Task<GetAllRolePermissionsByRoleIdResponse> GetAllPermissionsByRoleIdAndPermissionName(GetAllRolePermissionsByRoleIdAndPermissionNameRequest request, ServerCallContext context)
        {
            try
            {
                List<RolePermissionDto> list = await _rolePermissionService.GetPermissionByRoleIdAndPermissionName(request.RoleId, request.PermissionName);

                GetAllRolePermissionsByRoleIdResponse response = new GetAllRolePermissionsByRoleIdResponse();
                response.RolePermissions.AddRange(list.Select(x => new RolePermissionDto
                {
                    Id = x.Id,
                    RoleId = x.RoleId,
                    PermissionId = x.PermissionId,
                    RoleName = x.RoleName,
                    PermissionName = x.PermissionName
                }));

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

        [RequiresPermission("permission.create")]
        public override async Task<CreateRolePermissionResponse> CreateRolePermission(CreateRolePermissionRequest request, ServerCallContext context)
        {
            try
            {
                RolePermission model = Mapper.CreateRolePermissionModelFromDto(new RolePermissionDto
                {
                    Id = request.Model.Id,
                    RoleId = request.Model.RoleId,
                    PermissionId = request.Model.PermissionId
                });

                RolePermissionDto created = await _rolePermissionService.CreateRolePermission(Mapper.CreateRolePermissionDtoFromModel(model), request.UserId);

                return new CreateRolePermissionResponse
                {
                    RolePermission = new RolePermissionDto
                    {
                        Id = created.Id,
                        RoleId = created.RoleId,
                        PermissionId = created.PermissionId,
                        RoleName = created.RoleName,
                        PermissionName = created.PermissionName
                    }
                };
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to create role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

        [RequiresPermission("permission.update")]
        public override async Task<UpdateRolePermissionResponse> UpdateRolePermission(UpdateRolePermissionRequest request, ServerCallContext context)
        {
            try
            {
                RolePermission model = Mapper.CreateRolePermissionModelFromDto(new RolePermissionDto
                {
                    Id = request.Model.Id,
                    RoleId = request.Model.RoleId,
                    PermissionId = request.Model.PermissionId
                });

                RolePermissionDto updated = await _rolePermissionService.UpdateRolePermission(Mapper.CreateRolePermissionDtoFromModel(model), request.UserId);

                return new UpdateRolePermissionResponse
                {
                    RolePermission = new RolePermissionDto
                    {
                        Id = updated.Id,
                        RoleId = updated.RoleId,
                        PermissionId = updated.PermissionId,
                        RoleName = updated.RoleName,
                        PermissionName = updated.PermissionName
                    }
                };
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to update role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

        [RequiresPermission("permission.delete")]
        public override async Task<DeleteRolePermissionResponse> DeleteRolePermission(DeleteRolePermissionRequest request, ServerCallContext context)
        {
            try
            {
                await _rolePermissionService.DeleteRolePermission(request.Id, request.UserId);

                return new DeleteRolePermissionResponse { Success = true };
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to delete role permission. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

        //Private functions
        private UserModel ConvertRequestToModel(CreateUserRequest r)
        {
            return new UserModel
            {
                Id = r.Id,
                Username = r.Username,
                Email = r.Email,
                Password = r.Password,
                MobileNo = r.MobileNo,
                RoleId = r.RoleId,
                IsDeleted = Convert.ToBoolean(r.IsDeleted)
            };
        }

        private CreateUserRequest ConvertModelToRequest(UserModel r)
        {
            return new CreateUserRequest
            {
                Id = r.Id,
                Username = r.Username,
                Email = r.Email,
                Password = r.Password,
                MobileNo = r.MobileNo,
                RoleId = r.RoleId,
                IsDeleted = Convert.ToInt32(r.IsDeleted)
            };
        }

        private UserLoginResponse ToGrpcResponse(LoginResponseDto dto)
        {
            if (dto == null)
                return new UserLoginResponse
                {
                    UserId = 0,
                    ErrorMessage = "Response object was null"
                };

            return new UserLoginResponse
            {
                UserId = dto.UserId,
                Username = dto.Username ?? string.Empty,
                AccessToken = dto.AccessToken ?? string.Empty,
                RoleId = dto.RoleId,
                RoleName = dto.RoleName ?? string.Empty,
                ErrorMessage = dto.ErrorMessage ?? string.Empty
            };
        }
    }
}
