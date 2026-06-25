using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace UserServiceGrpc.Authorization
{
    public class RequiresPermissionAttribute : AuthorizeAttribute
    {
        public RequiresPermissionAttribute(string permissionTag) : base($"{permissionTag}")
        {

        }
    }

    public class AuthorizationPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _defPolicyProvider;
        private readonly ILogger<AuthorizationPolicyProvider> _logger;

        ConcurrentDictionary<string, AuthorizationPolicy> _policyDictionary;

        public AuthorizationPolicyProvider(IOptions<AuthorizationOptions> options, ILogger<AuthorizationPolicyProvider> logger)
        {
            _defPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
            _logger = logger;

            _policyDictionary = new ConcurrentDictionary<string, AuthorizationPolicy>();

            _logger.LogInformation("UserService AUTH POLICY PROVIDER loaded");
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
            string permissionName = policyName.ToLower();

            _logger.LogInformation("Processing policy: {policy}", permissionName);

            if (!_policyDictionary.ContainsKey(permissionName))
            {
                _logger.LogInformation("Policy not found in dictionary. Creating and adding now");

                AuthorizationPolicy policyToSend = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new RolePermissionRequirement(permissionName))
                    .Build();

                _policyDictionary.TryAdd(permissionName, policyToSend);

                _logger.LogInformation("Added policy for role permission:{pName}", permissionName);

                return await Task.FromResult<AuthorizationPolicy?>(policyToSend);
            }

            _policyDictionary.TryGetValue(permissionName, out AuthorizationPolicy policy);

            _logger.LogInformation("Fetched policy for role permission:{pName}", permissionName);

            return await Task.FromResult<AuthorizationPolicy?>(policy);
        }
    }
}
