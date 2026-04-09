using Bank.Application.Interface;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Dtos.SettingsModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Bank.Application.Services
{
    public class JwtService : IJwtService
    {
        private JwtSettings _jwtOptions;
        private readonly ILogger<JwtService> _logger;

        public JwtService(IOptions<JwtSettings> jwtOptions, ILogger<JwtService> logger)
        {
            _jwtOptions = jwtOptions.Value;
            _logger = logger;
        }

        public ApiResponse<string> GenerateJwtToken(string username)
        {
            try
            {
                var claims = new[]
                  {
                    new Claim(JwtRegisteredClaimNames.Sub, username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var token = new JwtSecurityToken(
                    issuer: _jwtOptions.Issuer,
                    audience: _jwtOptions.Audience,
                    claims: claims,
                    expires: DateTime.Now.AddMinutes(_jwtOptions.DurationInMinutes),
                    signingCredentials: creds);

                var jwtToken = new JwtSecurityTokenHandler().WriteToken(token);
                return ApiResponse<string>.Ok(jwtToken, "Token generated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user {Username}", username);
                return ApiResponse<string>.Fail("Failed to generate token.");
            }
        }
    }
}
