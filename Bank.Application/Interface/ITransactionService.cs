using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Bank.Domain.Enums;

namespace Bank.Application.Interface
{
    public interface ITransactionService
    {
        Task<ApiResponse<Transaction>> CreateTransaction(TransferRequest transferRequest, TransactionType transactionType, string userId);
        Task<ApiResponse<Transaction>> GetTransactionById(string transactionId);
        Task<ApiResponse<Transaction>> UpdateTransaction(Transaction transaction);
    }
}
