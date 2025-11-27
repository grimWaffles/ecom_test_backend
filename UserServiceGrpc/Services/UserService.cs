using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.IdentityModel.Tokens;
using UserServiceGrpc.Helpers;
using UserServiceGrpc.Models.Entities;
using UserServiceGrpc.Repository;

namespace UserServiceGrpc.Services
{
    public class UserService : User.UserBase
    {
        private readonly IUserRepository _repo;
        private readonly IConfiguration _configuration;

        public UserService(IUserRepository userRepository, IConfiguration configuration)
        {
            _repo = userRepository;
            _configuration = configuration;
        }

        //Test Functions
        public override async Task<TestResponse> TestService(Empty request, ServerCallContext context)
        {
            TestResponse response = new TestResponse();
            response.ServiceStatus = "Service is running.";

            return await Task.FromResult(response);
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

        private string GenerateJwtToken(UserModel user)
        {
            //Generate a GUID for the token and save it for later
            string guID = Guid.NewGuid().ToString();

            //Add the necessary claims to the token
            var claims = new[]{
                new Claim("UserId", Convert.ToString(user.Id)),
                new Claim("RoleId", Convert.ToString(user.RoleId)),
                new Claim("Username", Convert.ToString(user.Username)),
                new Claim(ClaimTypes.Role,user.Role.Name.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, guID)
            };

            //Generate Key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:signingKey"]));

            //Generate the credentials
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //Issue the token
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:validIssuer"],
                audience: _configuration["Jwt:validAudience"],
                claims: claims,
                expires: DateTime.Now.AddHours(24),
                signingCredentials: creds
            );

            try
            {
                string newToken = new JwtSecurityTokenHandler().WriteToken(token);
                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception e)
            {
                return "";
            }
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
            UserModel user = await _repo.GetUserById(request.Id);

            if (user == null)
            {
                return new CreateUserRequest()
                {
                    Id = 0
                };
            }

            return ConvertModelToRequest(user);
        }

        public override async Task<UserResponseMultiple> GetAllUsers(Empty request, ServerCallContext context)
        {
            List<UserModel> users = await _repo.GetUsers();

            if (users == null || users.Count == 0) { return new UserResponseMultiple(); }

            UserResponseMultiple usersResponse = new UserResponseMultiple();

            usersResponse.Users.AddRange(users.Select(r=> ConvertModelToRequest(r)).ToList());

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
            response.AccessToken = GenerateJwtToken(model);
            response.RoleId = model.RoleId;
            response.RoleName = model.Role.Name.ToString();
            response.ErrorMessage = "";

            return response;
        }

        public override async Task<UserLoginResponse> LogoutUser(UserRequestSingle request, ServerCallContext context)
        {
            UserLoginResponse response = new UserLoginResponse();
            response.UserId = request.UserId;
            response.ErrorMessage = "Logout successful";

            return response;
        }
        
    }
}
