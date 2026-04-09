using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;

namespace Bank.Application.Interface
{
    public interface IFinacialService
    {
        Task<ApiResponse<string>> CreditAccount(TransferRequest transferRequest);
        Task<ApiResponse<string>> Transfer(TransferRequest transferRequest);
    }
}
