using API_Gateway.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace API_Gateway.AuthHandlers
{
    public class RolePermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private const string POLICY_PREFIX = "Permission:";
        private readonly DefaultAuthorizationPolicyProvider _defPolicyProvider;
        private readonly ILogger<RolePermissionPolicyProvider> _logger;

        public RolePermissionPolicyProvider(IOptions<AuthorizationOptions> options, ILogger<RolePermissionPolicyProvider> logger)
        {
            _defPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
            _logger = logger;

            _logger.LogInformation("CREATED CUSTOMER AUTH POLICY PROVIDER");
        }

        public async Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        {
            return await _defPolicyProvider.GetDefaultPolicyAsync();
        }

        public async Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        {
            return await _defPolicyProvider.GetFallbackPolicyAsync();
        }

        public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        { 
            if (!policyName.StartsWith(POLICY_PREFIX))
            {
                _logger.LogInformation("Forwarding to appropriate policy");
                return await _defPolicyProvider.GetPolicyAsync(policyName);
            }
            
            string permissionName = policyName.Replace(POLICY_PREFIX, "");

            _logger.LogInformation("Processing policy: {policy}", permissionName);

            AuthorizationPolicy policyToSend = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new RolePermissionRequirement(permissionName))
                .Build();

            _logger.LogInformation("Added policy for role permission:{pName}", permissionName);

            return await Task.FromResult<AuthorizationPolicy?>(policyToSend);
        }
    }
}
