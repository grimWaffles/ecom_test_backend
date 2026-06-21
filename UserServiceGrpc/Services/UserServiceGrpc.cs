using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models.Dtos;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    [Authorize]
    public class UserGrpcService : User.UserBase
    {
        private readonly IUserRepository _repo;
        private readonly ILogger<UserGrpcService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IRolePermissionService _rolePermissionService;

        public UserGrpcService(IUserRepository userRepository, IConfiguration configuration, ILogger<UserGrpcService> logger, IRolePermissionService rolePermissionService)
        {
            _repo = userRepository;
            _logger = logger;
            _configuration = configuration;
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

        //CRUD Operations
        public override async Task<UserCrudResponse> CreateUser(CreateUserRequest request, ServerCallContext context)
        {
            UserModel userModel = await _repo.GetUserByUsername(request.Username);
            UserCrudResponse response = new UserCrudResponse();

            if (userModel != null)
            {
                response.Status = 0;

                if (userModel.Username != request.Username)
                {
                    response.ErrorMesage = response.ErrorMesage.IsNullOrEmpty() ? "Username already exists" : response.ErrorMesage + " | " + "Username already exists";
                }
                if (userModel.Email == request.Email)
                {
                    response.ErrorMesage = response.ErrorMesage.IsNullOrEmpty() ? "Email ID already exists" : response.ErrorMesage + " | " + "Email ID already exists";
                }
                if (userModel.MobileNo == request.MobileNo)
                {
                    response.ErrorMesage = response.ErrorMesage.IsNullOrEmpty() ? "Mobile number already exists" : response.ErrorMesage + " | " + "Mobile number already exists";
                }

                return response;
            }

            UserModel requestModel = ConvertRequestToModel(request);
            requestModel.CreatedBy = requestModel.RoleId == 1 ? 1 : request.UserId; requestModel.CreatedDate = DateTime.Now;

            int status = await _repo.CreateUser(requestModel);

            response.Status = status;
            response.ErrorMesage = status == 1 ? "User added successfully" : "Failed to add user";

            return response;
        }

        public override async Task<UserCrudResponse> UpdateUser(CreateUserRequest request, ServerCallContext context)
        {
            UserModel userModel = await _repo.GetUserById(request.Id);
            UserCrudResponse response = new UserCrudResponse();

            if (userModel == null)
            {
                response.Status = 0;
                response.ErrorMesage = "User does not exist";
                return response;
            }

            UserModel requestModel = ConvertRequestToModel(request);

            userModel.Username = requestModel.Username;
            userModel.Password = requestModel.Password;
            userModel.Email = requestModel.Email;
            userModel.MobileNo = requestModel.MobileNo;
            userModel.RoleId = requestModel.RoleId;

            userModel.ModifiedBy = request.UserId; userModel.ModifiedDate = DateTime.Now;

            int status = await _repo.UpdateUser(userModel);

            response.Status = status;
            response.ErrorMesage = status == 1 ? "User updated successfully" : "Failed to update user";

            return response;
        }

        public override async Task<UserCrudResponse> DeleteUser(UserRequestSingle request, ServerCallContext context)
        {
            UserModel userModel = await _repo.GetUserById(request.Id);
            UserCrudResponse response = new UserCrudResponse();

            if (userModel == null)
            {
                response.Status = 0;
                response.ErrorMesage = "User does not exist";
                return response;
            }

            userModel.IsDeleted = true; userModel.ModifiedDate = DateTime.Now; ; userModel.ModifiedBy = request.UserId;

            int status = await _repo.UpdateUser(userModel);

            response.Status = status;
            response.ErrorMesage = status == 1 ? "User updated successfully" : "Failed to update user";

            return response;
        }

        public override async Task<CreateUserRequest> GetUserByIdAsync(UserRequestSingle request, ServerCallContext context)
        {
            UserModel user = await _repo.GetUserById(request.UserId);

            if (user == null)
            {
                return null;
            }

            return ConvertModelToRequest(user);
        }

        public override async Task<UserResponseMultiple> GetAllUsers(Empty request, ServerCallContext context)
        {
            int userId = Convert.ToInt32(TokenHelper.GetClaimValueFromToken(context.GetHttpContext(),"UserId"));

            List<UserModel> users = await _repo.GetUsers();

            if (users == null || users.Count == 0) { return new UserResponseMultiple(); }

            UserResponseMultiple usersResponse = new UserResponseMultiple();

            usersResponse.Users.AddRange(users.Select(r => ConvertModelToRequest(r)).ToList());

            return usersResponse;
        }

        public override async Task GetAllUsersStream(Empty request, IServerStreamWriter<CreateUserRequest> responseStream, ServerCallContext context)
        {
            CreateUserRequest response = new CreateUserRequest();

            try
            {
                List<UserModel> userModels = await _repo.GetUsers();

                foreach (UserModel user in userModels)
                {
                    await responseStream.WriteAsync(ConvertModelToRequest(user));
                }
            }
            catch (Exception e)
            {
                response.Id = 0;
                await responseStream.WriteAsync(response);
            }
        }

        //User Authentication
        [AllowAnonymous]
        public override async Task<UserLoginResponse> LoginUser(UserLoginRequest request, ServerCallContext context)
        {
            UserLoginResponse response = new UserLoginResponse();
            UserModel model = await _repo.GetUserByUsername(request.Username);

            if (model == null)
            {
                response.UserId = 0;
                response.ErrorMessage = "User does not exist";

                return response;
            }

            ///Todo
            /// Hash the password before checking

            if (model.Password != request.Password)
            {
                response.UserId = 0;
                response.ErrorMessage = "Password is incorrect";

                return response;
            }

            response.UserId = model.Id;
            response.Username = model.Username;
            response.AccessToken = GenerateJwtTokenForUser(model);
            response.RoleId = model.RoleId;
            response.RoleName = model.Role.Name.ToString();
            response.ErrorMessage = "";

            return response;
        }

        [AllowAnonymous]
        public override async Task<UserLoginResponse> LogoutUser(UserRequestSingle request, ServerCallContext context)
        {
            UserLoginResponse response = new UserLoginResponse();
            response.UserId = request.UserId;
            response.ErrorMessage = "Logout successful";

            return response;
        }

        //Role Permissions
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

        public override async Task<CheckRoleIdAndPermissionResponse> CheckRoleIdAndPermission(CheckRoleIdAndPermissionRequest request, ServerCallContext context)
        {
            try
            {
                bool exists = await _rolePermissionService.CheckRoleIdAndPermissionName(request.RoleId, request.PermissionName);

                CheckRoleIdAndPermissionResponse response = new CheckRoleIdAndPermissionResponse()
                {
                    Exists = exists
                };

                return response;
            }
            catch (Exception e)
            {
                _logger.LogError("Error: Failed to fetch role permissions. Message: {message}. StackTrace: {stacktrace}", e.Message, e.StackTrace);
                throw new RpcException(new Status(StatusCode.Internal, e.Message));
            }
        }

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

        private string GenerateJwtTokenForUser(UserModel user)
        {
            //Claims dictionary<type,value(in string)>
            Dictionary<string, string> claimDictionary = new Dictionary<string, string>();
            claimDictionary.Add("UserId", user.Id.ToString());
            claimDictionary.Add("RoleId", user.RoleId.ToString());
            claimDictionary.Add("Username", user.Username);
            claimDictionary.Add("Role",user.Role.Name.ToString().ToUpper());

            return TokenHelper.GenerateJwtToken(
                claimDictionary,
                _configuration["JwtUserSchema:signingKey"] ?? "",
                _configuration["JwtUserSchema:validIssuer"] ?? "", 
                _configuration["JwtUserSchema:validAudience"] ?? "",
                _configuration["JwtUserSchema:ExpirationInSeconds"] ?? ""
            );
        }

        //private async Task<int> GetUserIdFromToken(HttpContext httpContext)
        //{
        //    try
        //    {
        //        string userIdClaim = httpContext.User.Claims.Where(x => x.Type == "UserId").First().Value ?? "";
        //        return await Task.FromResult(Convert.ToInt32(userIdClaim));
        //    }
        //    catch (Exception e)
        //    {
        //        return await Task.FromResult(-1);
        //    }
        //}
    }
}
