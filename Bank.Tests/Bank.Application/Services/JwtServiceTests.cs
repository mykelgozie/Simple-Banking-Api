using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Bank.Application.Services;
using Bank.Domain.Dtos.SettingsModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Bank.Tests
{
    public class JwtServiceTests
    {
        private readonly Mock<ILogger<JwtService>> _loggerMock;

        public JwtServiceTests()
        {
            _loggerMock = new Mock<ILogger<JwtService>>();
        }

        [Fact]
        public void GenerateJwtToken_ReturnsToken_OnSuccess()
        {
            // Arrange
            var settings = new JwtSettings
            {
                Key = "supersecretkey-verylong-and-random",
                Issuer = "test-issuer",
                Audience = "test-audience",
                DurationInMinutes = 60
            };
            var options = Options.Create(settings);
            var svc = new JwtService(options, _loggerMock.Object);

            // Act
            var result = svc.GenerateJwtToken("alice");

            // Assert
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.Data));
            Assert.Contains("Token generated", result.Message, StringComparison.OrdinalIgnoreCase);

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(result.Data);

            var sub = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            Assert.Equal("alice", sub);
        }

        [Fact]
        public void GenerateJwtToken_ReturnsFail_WhenKeyMissing()
        {
            // Arrange - missing key should cause an exception during token creation
            var settings = new JwtSettings
            {
                Key = null,
                Issuer = "test-issuer",
                Audience = "test-audience",
                DurationInMinutes = 60
            };
            var options = Options.Create(settings);
            var svc = new JwtService(options, _loggerMock.Object);

            // Act
            var result = svc.GenerateJwtToken("bob");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to generate token", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}