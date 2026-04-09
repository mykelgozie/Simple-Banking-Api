using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private IAccountService _accountService;
        private IAuthService _authService;

        public AccountController(IAccountService accountService, IAuthService authService)
        {
            _accountService = accountService;
            _authService = authService;
        }

        [HttpGet("balance/{userId}")]
        public async Task<IActionResult> GetAccountBalance(string userId)
        {
            var result = await _accountService.GetAccountByUserIdAsync(userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpGet("profile/{userId}")]
        public async Task<IActionResult> GetByUserID(string userId)
        {
            var result = await _authService.GetUserByUserId(userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpPut("profile/{userId}")]
        public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserRequest updateUserRequest)
        {
            var result = await _authService.UpdateUser(updateUserRequest, userId);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
