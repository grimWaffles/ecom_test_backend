
using API_Gateway.AuthHandlers.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace API_Gateway.AuthHandlers.PolicyProviders
{
    public class RequiresPermissionAttribute : AuthorizeAttribute
    {
        public RequiresPermissionAttribute(string permissionTag) : base($"{permissionTag}")
        {

        }
    }

    public class RolePermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private const string POLICY_PREFIX = "Permission:";

        private readonly DefaultAuthorizationPolicyProvider _defPolicyProvider;
        private readonly ILogger<RolePermissionPolicyProvider> _logger;

        ConcurrentDictionary<string, AuthorizationPolicy> _policyDictionary;

        public RolePermissionPolicyProvider(IOptions<AuthorizationOptions> options, ILogger<RolePermissionPolicyProvider> logger)
        {
            _defPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
            _logger = logger;

            _policyDictionary = new ConcurrentDictionary<string, AuthorizationPolicy>();

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
            //if (!policyName.StartsWith(POLICY_PREFIX))
            //{
            //    _logger.LogInformation("Forwarding to appropriate policy");
            //    return await _defPolicyProvider.GetPolicyAsync(policyName);
            //}

            string permissionName = policyName.ToLower(); //policyName.Replace(POLICY_PREFIX, "");

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
