using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace ProductServiceGrpc.Helpers
{
    public interface ITokenHelper
    {
        string? GetClaimValueFromToken(string claimType);
    }

    public class TokenHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TokenHelper(IHttpContextAccessor contextAccessor)
        {
            _httpContextAccessor = contextAccessor;
        }

        public string? GetClaimValueFromToken(string claimType)
        {
            try
            {
                return _httpContextAccessor.HttpContext?.User.Claims.Where(x => x.Type == claimType).First().Value ?? "";
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
