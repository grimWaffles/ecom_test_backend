using API_Gateway.Models;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API_Gateway.Helpers
{
    public interface ITokenHelper
    {
        string? GetClaimValueFromToken(string claimType);
        void GenerateAndStoreServiceToken(int roleId, string permission);
        string? GetUserToken();
        Metadata? GetGrpcHeaders();
    }

    public class TokenHelper : ITokenHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JwtInternalSchemaOptions _jwtInternalSchemaOptions;

        public TokenHelper(IHttpContextAccessor contextAccessor, IOptions<JwtInternalSchemaOptions> schemaOptions)
        {
            _httpContextAccessor = contextAccessor;
            _jwtInternalSchemaOptions = schemaOptions.Value;
        }

        public string? GetUserToken()
        {
            return _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault() ?? ""; ;
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

        public void GenerateAndStoreServiceToken(int roleId, string permission)
        {
            if (_httpContextAccessor.HttpContext == null) return;

            //Generate a GUID for the token and save it for later
            string guId = Guid.NewGuid().ToString();

            Claim[] claims = new[]
            {
                new Claim("RoleId", roleId.ToString()),
                new Claim("Permission", permission),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            string token = GenerateJwtToken(claims);

            Metadata headers = new Metadata { { "Authorization", $"Bearer {token}" } };

            _httpContextAccessor.HttpContext.Items["GrpcHeaders"] = headers;
        }

        public Metadata? GetGrpcHeaders()
        {
            return _httpContextAccessor.HttpContext?.Items["GrpcHeaders"] as Metadata;
        }

        private string GenerateJwtToken(Claim[] claims)
        {
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtInternalSchemaOptions.SigningKey));
            var signingCreds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            //Issue the token
            var token = new JwtSecurityToken(
                issuer: _jwtInternalSchemaOptions.ValidIssuer,
                audience: _jwtInternalSchemaOptions.ValidAudience,
                claims: claims,
                expires: DateTime.Now.AddSeconds(_jwtInternalSchemaOptions.ExpirationInSeconds),
                signingCredentials: signingCreds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
