using Grpc.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserServiceGrpc.Models;
using UserServiceGrpc.Models.Entities;

namespace UserServiceGrpc.Helpers
{
    public interface ITokenHelper
    {
        string? GetClaimValueFromToken(string claimType);
        string GenerateJwtToken(Claim[] claimsArray);
    }

    public class TokenHelper : ITokenHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JwtUserSchemaOptions _jwtUserSchemaOptions;

        public TokenHelper(IHttpContextAccessor contextAccessor, IOptions<JwtUserSchemaOptions> schemaOptions)
        {
            _httpContextAccessor = contextAccessor;
            _jwtUserSchemaOptions = schemaOptions.Value;
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

        public string GenerateJwtToken(Claim[] claimsArray)
        {
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtUserSchemaOptions.SigningKey));
            var signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            //Issue the token
            var token = new JwtSecurityToken(
                issuer: _jwtUserSchemaOptions.ValidIssuer,
                audience: _jwtUserSchemaOptions.ValidAudience,
                claims: claimsArray,
                expires: DateTime.Now.AddSeconds(_jwtUserSchemaOptions.ExpirationInSeconds),
                signingCredentials: signingCreds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
