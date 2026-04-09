using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;

namespace Bank.Application.Interface
{
    public interface IAccountService
    {
        Task<ApiResponse<Account>> CreateAccountAsync(string userId);
        Task<ApiResponse<bool>> CreditAccount(string recieverAccountNumber, decimal amount);
        Task<ApiResponse<bool>> DebitAccount(string senderAccountNumber, decimal amount);
        Task<ApiResponse<Account>> GetAccountByUserIdAsync(string userId);
        Task<ApiResponse<string>> ValidateBankAccount(string senderAcountNumber, string recieverAccountNumber);
    }
}
