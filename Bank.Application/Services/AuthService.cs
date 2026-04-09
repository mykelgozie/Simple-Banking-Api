using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Bank.Infrastructure.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services
{
    public class AuthService : IAuthService
    {
        private UserManager<ApplicationUser> _userManager;
        private SignInManager<ApplicationUser> _signInManager;
        private IJwtService _jwtService;
        private IAccountService _accountService;
        private ILogger<AuthService> _logger;

        public AuthService(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IJwtService jwtService, IAccountService accountService, ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _accountService = accountService;
            _logger = logger;
        }


        public async Task<ApiResponse<RegisterUserResponse>> RegisterAsync(RegisterUserRequest registerUserRequest)
        {
            try
            {
                var user = new ApplicationUser
                {
                    UserName = registerUserRequest.Email,
                    Email = registerUserRequest.Email,
                    FirstName = registerUserRequest.FirstName,
                    LastName = registerUserRequest.LastName
                };

                var result = await _userManager.CreateAsync(user, registerUserRequest.Password);
                if (result.Succeeded)
                {
                    _ = await _accountService.CreateAccountAsync(user.Id);
                    var response = new RegisterUserResponse
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName
                    };
                    return ApiResponse<RegisterUserResponse>.Ok(response, "User registered successfully.");
                }
                else
                {
                    var errors = result.Errors.Select(e => e.Description).ToList();
                    return ApiResponse<RegisterUserResponse>.Fail("User registration failed.", errors);
                }
            }
            catch (Exception)
            {
                return ApiResponse<RegisterUserResponse>.Fail("An error occurred while registering the user.");
            }
        }


        public async Task<ApiResponse<LoginUserResponse>> LoginAsync(LoginUserRequest loginUserRequest)
        {
            try
            {
                _logger.LogInformation("Attempting to log in user with email: {Email}", loginUserRequest.Email);
                var result = await _signInManager.PasswordSignInAsync(loginUserRequest.Email, loginUserRequest.Password, false, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User with email {Email} logged in successfully.", loginUserRequest.Email);

                    var jwtResponse = _jwtService.GenerateJwtToken(loginUserRequest.Email);
                    if (!jwtResponse.Success)
                        return ApiResponse<LoginUserResponse>.Fail("Failed to login user.");


                    _logger.LogInformation("JWT token generated successfully for user with email: {Email}", loginUserRequest.Email);

                    var response = new LoginUserResponse
                    {
                        Token = jwtResponse.Data
                    };

                    return ApiResponse<LoginUserResponse>.Ok(response, "User logged in successfully.");
                }
                else
                {
                    return ApiResponse<LoginUserResponse>.Fail("Invalid email or password.");
                }
            }
            catch (Exception)
            {
                return ApiResponse<LoginUserResponse>.Fail("An error occurred while logging in the user.");
            }
        }


        public async Task<ApiResponse<RegisterUserResponse>> GetUserByEmail(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    return ApiResponse<RegisterUserResponse>.Fail("User not found.");
                }

                var response = new RegisterUserResponse
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email
                };

                return ApiResponse<RegisterUserResponse>.Ok(response, "User found successfully.");
            }
            catch (Exception)
            {
                return ApiResponse<RegisterUserResponse>.Fail("An error occurred while logging out the user.");
            }
        }


        public async Task<ApiResponse<RegisterUserResponse>> GetUserByUserId(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<RegisterUserResponse>.Fail("User not found.");
                }

                var response = new RegisterUserResponse
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    UserId = userId
                };

                return ApiResponse<RegisterUserResponse>.Ok(response, "User found successfully.");
            }
            catch (Exception)
            {
                return ApiResponse<RegisterUserResponse>.Fail("An error occurred while logging out the user.");
            }
        }

        public async Task<RegisterUserResponse> GetUserById(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return null;
                }

                return new RegisterUserResponse
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    UserId = user.Id
                };
            }
            catch (Exception)
            {

                _logger.LogError("An error occurred while logging out the user.");
                return null;
            }

        }

        public async Task<ApiResponse<RegisterUserResponse>> UpdateUser(UpdateUserRequest updateUserRequest, string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return ApiResponse<RegisterUserResponse>.Fail("User not found.");
                }

                user.FirstName = updateUserRequest.FirstName;
                user.LastName = updateUserRequest.LastName;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    var response = new RegisterUserResponse
                    {
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email
                    };
                    return ApiResponse<RegisterUserResponse>.Ok(response, "User updated successfully.");
                }

                return ApiResponse<RegisterUserResponse>.Fail("Failed to update user.");
            }
            catch (Exception)
            {
                return ApiResponse<RegisterUserResponse>.Fail("An error occurred while logging out the user.");
            }
        }
    }
}
