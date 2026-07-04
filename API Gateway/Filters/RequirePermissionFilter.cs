using API_Gateway.AuthHandlers.PolicyProviders;
using API_Gateway.Helpers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace API_Gateway.Filters
{
    public class RequirePermissionFilter : IAsyncActionFilter
    {
        private readonly ITokenHelper _tokenHelper;
        public RequirePermissionFilter(ITokenHelper tokenHelper)
        {
            _tokenHelper = tokenHelper;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            Console.WriteLine("**Filter in action: RequstPermissionFilter");
            
            RequiresPermissionAttribute? permissionRequired = context.ActionDescriptor.EndpointMetadata
                .OfType<RequiresPermissionAttribute>()
                .FirstOrDefault();

            if (permissionRequired != null)
            {
                string permissionToSet = permissionRequired.Policy ?? "";

                if (permissionToSet != "")
                {
                    int roleId = Convert.ToInt32(_tokenHelper.GetClaimValueFromToken("RoleId"));

                    _tokenHelper.GenerateAndStoreServiceToken(roleId, permissionToSet);
                }
            }

            await next();
        }
    }
}
