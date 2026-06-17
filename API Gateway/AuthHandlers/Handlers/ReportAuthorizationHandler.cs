using API_Gateway.Models;
using ApiGateway.Protos;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace API_Gateway.AuthHandlers.Handlers
{
    public class ReportResourceRequirement : IAuthorizationRequirement { }

    //Manual Authorization handlers that is called in each individual controller action
    public class ReportAuthorizationHandler : AuthorizationHandler<ReportResourceRequirement, ReportModel>
    {
        private readonly ILogger<ReportAuthorizationHandler> _logger;

        public ReportAuthorizationHandler(ILogger<ReportAuthorizationHandler> logger)
        {
            _logger = logger;
        }
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ReportResourceRequirement requirement, ReportModel resource)
        {
            _logger.LogInformation($"ReportResourceHandler is handling authorization for report with ID: {resource.ReportId} and UserID: {resource.OwnerId}");

            try
            {
                string claimType = "userid";

                var userIdClaim = context.User.FindFirst(c => c.Type.Equals(claimType, StringComparison.OrdinalIgnoreCase));

                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    if(resource.OwnerId == userId)
                    {
                        _logger.LogInformation($"Authorization succeeded for user ID: {userId} on report ID: {resource.ReportId}");
                        context.Succeed(requirement);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while handling authorization in ReportResourceHandler.");
                context.Fail();
            }
        }
    }
}
