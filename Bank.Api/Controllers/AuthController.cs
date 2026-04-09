using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest registerUserRequest)
        {
            var result = await _authService.RegisterAsync(registerUserRequest);
            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest loginUserRequest)
        {
            var result = await _authService.LoginAsync(loginUserRequest);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}
