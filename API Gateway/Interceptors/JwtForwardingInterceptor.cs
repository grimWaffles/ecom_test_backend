using API_Gateway.Helpers;
using ApiGateway.Protos;
using Azure.Core;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client.Configuration;
using Microsoft.EntityFrameworkCore;

namespace API_Gateway.Interceptors
{
    public class JwtForwardingInterceptor : Interceptor
    {
        private readonly IHttpContextAccessor _httpContext;
        private readonly IConfiguration _config;
        private readonly ILogger<JwtForwardingInterceptor> _logger;

        private static readonly Dictionary<string, string> _userServiceMethodPermissions = new()
        {
            // CRUD Operations
            { "GetUserByIdAsync",                                   "users.view" },
            { "GetAllUsersStream",                                  "users.view" },
            { "GetAllUsers",                                        "users.view" },
            { "CreateUser",                                         "users.create" },
            { "UpdateUser",                                         "users.update" },
            { "DeleteUser",                                         "users.delete" },

            // User Authentication
            { "LoginUser",                                          "" },
            { "LogoutUser",                                         "" },

            // Role Permissions - View
            { "GetAllPermissionsByRoleId",                          "permissions.view" },
            { "GetAllPermissionsByRoleIdAndPermissionName",         "permissions.view" },
            { "CheckRoleIdAndPermission",                           "permissions.view" },

            // Role Permissions - Write
            { "CreateRolePermission",                               "permissions.create" },
            { "UpdateRolePermission",                               "permissions.update" },
            { "DeleteRolePermission",                               "permissions.delete" },
        };

        public JwtForwardingInterceptor(IConfiguration configuration, IHttpContextAccessor contextAccessor, ILogger<JwtForwardingInterceptor> logger)
        {
            _httpContext = contextAccessor;
            _config = configuration;
            _logger = logger;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            _logger.LogInformation($"Interceptor hit — Method: {context.Method.Name}, Request type: {typeof(TRequest).Name}");

            //
            string authHeadersFromToken = _httpContext.HttpContext.Request.Headers["Authorization"].FirstOrDefault() ?? "";

            Metadata headers = new Metadata();

            headers.Add("Authorization", authHeadersFromToken);

            CallOptions options = context.Options.WithHeaders(headers);

            ClientInterceptorContext<TRequest, TResponse> newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);

            return continuation(request, newContext);
        }

        private string GetServiceToken(string permission)
        {
            //Claims dictionary<type,value(in string)>
            Dictionary<string, string> claimDictionary = new Dictionary<string, string>();
            claimDictionary.Add("RoleId", 1.ToString());
            claimDictionary.Add("Permission", permission);

            string token = TokenHelper.GenerateJwtToken(
                claimDictionary,
                _config["JwtInternalSchema:signingKey"] ?? "",
                _config["JwtInternalSchema:validIssuer"] ?? "",
                _config["JwtInternalSchema:validAudience"] ?? "",
                _config["JwtInternalSchema:ExpirationInSeconds"] ?? ""
            );

            return token;
        }

        private string GetUserToken()
        {
            return _httpContext.HttpContext.Request.Headers["Authorization"].FirstOrDefault() ?? ""; ;
        }
    }
}
