using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services
{
    public class TransactionService : ITransactionService
    {
        private IUnitOfWork _unitOfWork;
        private ILogger<TransactionService> _logger;

        public TransactionService(IUnitOfWork unitOfWork, ILogger<TransactionService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ApiResponse<Transaction>> CreateTransaction(TransferRequest transferRequest, TransactionType transactionType, string userId)
        {
            try
            {

                var transaction = new Transaction
                {
                    AccountNumber = TransactionType.Debit == transactionType ? transferRequest.SenderAccount : transferRequest.ReceiverAccount,
                    Amount = transferRequest.Amount,
                    TransactionType = transactionType,
                    Status = TransactionStatus.Pending,
                    Reference = GenerateTransactionReference(),
                    userId = userId,
                    TraansactionId = transferRequest.TransactionId
                };

                await _unitOfWork.TransactionRepository.AddAsync(transaction);
                await _unitOfWork.SaveAsync();

                return new ApiResponse<Transaction>
                {
                    Success = true,
                    Message = "Transaction created successfully",
                    Data = transaction
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction for TransactionId: {TransactionId}", transferRequest.TransactionId);
                await _unitOfWork.RollBackAsync();
                return new ApiResponse<Transaction>
                {
                    Success = false,
                    Message = "Failed to create transaction"
                };
            }
        }

        public async Task<ApiResponse<Transaction>> GetTransactionById(string transactionId)
        {
            try
            {
                var transaction = await _unitOfWork.TransactionRepository.GetFirstOrDefaultAsync(t => t.TraansactionId == transactionId);
                if (transaction == null)
                {
                    return new ApiResponse<Transaction>
                    {
                        Success = false,
                        Message = "Transaction not found",
                        Data = null
                    };
                }
                return new ApiResponse<Transaction>
                {
                    Success = true,
                    Message = "Transaction retrieved successfully",
                    Data = transaction
                };
            }
            catch (Exception)
            {
                return new ApiResponse<Transaction>
                {
                    Success = false,
                    Message = "Failed to retrieve transaction",
                    Data = null
                };
            }
        }

        public async Task<ApiResponse<Transaction>> UpdateTransaction(Transaction transaction)
        {
            try
            {

                _unitOfWork.TransactionRepository.Update(transaction);
                await _unitOfWork.SaveAsync();
                return new ApiResponse<Transaction>
                {
                    Success = true,
                    Message = "Transaction updated successfully",
                    Data = transaction
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction with TransactionId: {TransactionId}", transaction.TraansactionId);
                await _unitOfWork.RollBackAsync();
                return new ApiResponse<Transaction>
                {
                    Success = false,
                    Message = "Failed to update transaction",
                    Data = null
                };
            }

        }

        private string GenerateTransactionReference(int length = 11)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            return new string(Enumerable
                .Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());
        }

    }
}
