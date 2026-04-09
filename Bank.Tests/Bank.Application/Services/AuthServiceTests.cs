using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bank.Application.Services;
using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bank.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
        private readonly Mock<IJwtService> _jwtServiceMock;
        private readonly Mock<IAccountService> _accountServiceMock;
        private readonly Mock<ILogger<AuthService>> _loggerMock;

        public AuthServiceTests()
        {
            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null, null, null, null, null, null, null, null);

            var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            var userPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
                _userManagerMock.Object,
                httpContextAccessorMock.Object,
                userPrincipalFactoryMock.Object,
                null,
                new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
                null,
                null);

            _jwtServiceMock = new Mock<IJwtService>();
            _accountServiceMock = new Mock<IAccountService>();
            _loggerMock = new Mock<ILogger<AuthService>>();
        }

        private AuthService CreateService()
        {
            return new AuthService(
                _userManagerMock.Object,
                _signInManagerMock.Object,
                _jwtServiceMock.Object,
                _accountServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturnOk_WhenCreateSucceeds()
        {
            // Arrange
            var req = new RegisterUserRequest
            {
                Email = "alice@example.com",
                Password = "Password123!",
                FirstName = "Alice",
                LastName = "Smith"
            };

            _userManagerMock
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Success);

            _accountServiceMock
                .Setup(x => x.CreateAccountAsync(It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Domain.Entities.Account>.Ok(new Domain.Entities.Account(), "Created"));

            var svc = CreateService();

            // Act
            var result = await svc.RegisterAsync(req);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User registered successfully.", result.Message);
            Assert.NotNull(result.Data);
            Assert.Equal(req.FirstName, result.Data.FirstName);
            _userManagerMock.Verify(x => x.CreateAsync(It.Is<ApplicationUser>(u => u.Email == req.Email), req.Password), Times.Once);
            _accountServiceMock.Verify(x => x.CreateAccountAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturnFail_WhenCreateReturnsErrors()
        {
            // Arrange
            var req = new RegisterUserRequest { Email = "bob@example.com", Password = "pw", FirstName = "Bob", LastName = "Jones" };

            var identityErrors = new[] { new IdentityError { Description = "Bad password" } };
            _userManagerMock
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(identityErrors));

            var svc = CreateService();

            // Act
            var result = await svc.RegisterAsync(req);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("User registration failed", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Errors);
            Assert.Contains("Bad password", result.Errors);
            _accountServiceMock.Verify(x => x.CreateAccountAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturnFail_OnException()
        {
            // Arrange
            var req = new RegisterUserRequest { Email = "err@example.com", Password = "pw", FirstName = "E", LastName = "R" };

            _userManagerMock
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("boom"));

            var svc = CreateService();

            // Act
            var result = await svc.RegisterAsync(req);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("An error occurred while registering", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnOk_WhenSignInAndJwtSucceed()
        {
            // Arrange
            var req = new LoginUserRequest { Email = "alice@example.com", Password = "Password123!" };

            _signInManagerMock
                .Setup(s => s.PasswordSignInAsync(req.Email, req.Password, false, false))
                .ReturnsAsync(SignInResult.Success);

            _jwtServiceMock
                .Setup(j => j.GenerateJwtToken(req.Email))
                .Returns(ApiResponse<string>.Ok("token-value", "Token generated"));

            var svc = CreateService();

            // Act
            var result = await svc.LoginAsync(req);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User logged in successfully.", result.Message);
            Assert.Equal("token-value", result.Data.Token);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnFail_WhenInvalidCredentials()
        {
            // Arrange
            var req = new LoginUserRequest { Email = "nope@example.com", Password = "wrong" };

            _signInManagerMock
                .Setup(s => s.PasswordSignInAsync(req.Email, req.Password, false, false))
                .ReturnsAsync(SignInResult.Failed);

            var svc = CreateService();

            // Act
            var result = await svc.LoginAsync(req);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Invalid email or password", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnFail_WhenJwtGenerationFails()
        {
            // Arrange
            var req = new LoginUserRequest { Email = "alice@example.com", Password = "Password123!" };

            _signInManagerMock
                .Setup(s => s.PasswordSignInAsync(req.Email, req.Password, false, false))
                .ReturnsAsync(SignInResult.Success);

            _jwtServiceMock
                .Setup(j => j.GenerateJwtToken(req.Email))
                .Returns(ApiResponse<string>.Fail("Failed to generate token."));

            var svc = CreateService();

            // Act
            var result = await svc.LoginAsync(req);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to login user", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserByEmail_ShouldReturnOk_WhenUserFound()
        {
            // Arrange
            var user = new ApplicationUser { Id = "u1", Email = "test@example.com", FirstName = "T", LastName = "User" };

            _userManagerMock
                .Setup(x => x.FindByEmailAsync(user.Email))
                .ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var result = await svc.GetUserByEmail(user.Email);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(user.Email, result.Data.Email);
            Assert.Equal(user.FirstName, result.Data.FirstName);
            Assert.Equal(user.LastName, result.Data.LastName);
        }

        [Fact]
        public async Task GetUserByEmail_ShouldReturnFail_WhenNotFound()
        {
            // Arrange
            _userManagerMock
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser)null);

            var svc = CreateService();

            // Act
            var result = await svc.GetUserByEmail("missing@example.com");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("User not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetUserByUserId_ShouldReturnOk_WhenUserFound()
        {
            // Arrange
            var user = new ApplicationUser { Id = "id-123", Email = "x@y.com", FirstName = "F", LastName = "L" };

            _userManagerMock
                .Setup(x => x.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var result = await svc.GetUserByUserId(user.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(user.Email, result.Data.Email);
            Assert.Equal(user.FirstName, result.Data.FirstName);
        }

        [Fact]
        public async Task GetUserById_ShouldReturnResponse_WhenFound()
        {
            // Arrange
            var user = new ApplicationUser { Id = "u-1", Email = "a@b.com", FirstName = "First", LastName = "Last" };

            _userManagerMock
                .Setup(x => x.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            var svc = CreateService();

            // Act
            var result = await svc.GetUserById(user.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);
            Assert.Equal(user.Email, result.Email);
        }

        [Fact]
        public async Task GetUserById_ShouldReturnNull_WhenNotFound()
        {
            // Arrange
            _userManagerMock
                .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser)null);

            var svc = CreateService();

            // Act
            var result = await svc.GetUserById("missing");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateUser_ShouldReturnOk_WhenUpdateSucceeds()
        {
            // Arrange
            var user = new ApplicationUser { Id = "u123", Email = "u@u.com", FirstName = "Old", LastName = "Name" };
            var updateReq = new UpdateUserRequest { FirstName = "New", LastName = "Name2" };

            _userManagerMock
                .Setup(x => x.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(x => x.UpdateAsync(It.Is<ApplicationUser>(u => u.Id == user.Id && u.FirstName == updateReq.FirstName && u.LastName == updateReq.LastName)))
                .ReturnsAsync(IdentityResult.Success);

            var svc = CreateService();

            // Act
            var result = await svc.UpdateUser(updateReq, user.Id);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("User updated successfully.", result.Message);
            Assert.Equal(updateReq.FirstName, result.Data.FirstName);
            Assert.Equal(updateReq.LastName, result.Data.LastName);
        }

        [Fact]
        public async Task UpdateUser_ShouldReturnFail_WhenUserNotFound()
        {
            // Arrange
            _userManagerMock
                .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser)null);

            var svc = CreateService();

            // Act
            var result = await svc.UpdateUser(new UpdateUserRequest { FirstName = "x", LastName = "y" }, "no-id");

            // Assert
            Assert.False(result.Success);
            Assert.Contains("User not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateUser_ShouldReturnFail_WhenUpdateFails()
        {
            // Arrange
            var user = new ApplicationUser { Id = "u1", Email = "u@u.com", FirstName = "A", LastName = "B" };
            var updateReq = new UpdateUserRequest { FirstName = "C", LastName = "D" };

            _userManagerMock
                .Setup(x => x.FindByIdAsync(user.Id))
                .ReturnsAsync(user);

            _userManagerMock
                .Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "update failed" }));

            var svc = CreateService();

            // Act
            var result = await svc.UpdateUser(updateReq, user.Id);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Failed to update user", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}