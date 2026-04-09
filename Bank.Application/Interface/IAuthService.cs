using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;

namespace Bank.Application.Interface
{
    public interface IAuthService
    {
        Task<ApiResponse<RegisterUserResponse>> GetUserByEmail(string email);
        Task<RegisterUserResponse> GetUserById(string userId);
        Task<ApiResponse<RegisterUserResponse>> GetUserByUserId(string userId);
        Task<ApiResponse<LoginUserResponse>> LoginAsync(LoginUserRequest loginUserRequest);
        Task<ApiResponse<RegisterUserResponse>> RegisterAsync(RegisterUserRequest registerUserRequest);
        Task<ApiResponse<RegisterUserResponse>> UpdateUser(UpdateUserRequest updateUserRequest, string userId);
    }
}
