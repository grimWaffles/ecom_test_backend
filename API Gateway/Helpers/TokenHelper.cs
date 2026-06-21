using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API_Gateway.Helpers
{
    public static class TokenHelper
    {
        public static string GetClaimValueFromToken(HttpContext httpContext, string claimType)
        {
            try
            {
                string claimValue = httpContext.User.Claims.Where(x => x.Type == claimType).First().Value ?? "";
                return claimValue;
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static string GenerateJwtToken(Dictionary<string, string> claimDictionary, string signingKey, string validIssuer, string validAudience, string expirationInSeconds)
        {
            int tokenExpiration = Convert.ToInt32(expirationInSeconds);

            //Generate a GUID for the token and save it for later
            string guId = Guid.NewGuid().ToString();

            //Add the necessary claims to the token
            var claims = claimDictionary
                .Select(x => new Claim(x.Key, x.Value))
                .Append(new Claim(JwtRegisteredClaimNames.Jti, guId))
                .ToArray();

            //Generate Key
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            //Generate the credentials
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            //Issue the token
            var token = new JwtSecurityToken(
                issuer: validIssuer,
                audience: validAudience,
                claims: claims,
                expires: DateTime.Now.AddSeconds(tokenExpiration),
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
    }
}
