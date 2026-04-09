using Bank.Domain.Dtos.Response;

namespace Bank.Application.Interface
{
    public interface IJwtService
    {
        ApiResponse<string> GenerateJwtToken(string username);
    }
}
