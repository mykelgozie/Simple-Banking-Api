using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services
{
    public class FinacialService : IFinacialService
    {
        private IUnitOfWork _unitOfWork;
        private ITransactionService _transactionService;
        private readonly IAccountService _accountService;
        private readonly ILogger<FinacialService> _logger;
        private readonly IAuthService _authService;

        public FinacialService(IUnitOfWork unitOfWork, ITransactionService transactionService, IAccountService accountService, ILogger<FinacialService> logger, IAuthService authService)
        {
            _unitOfWork = unitOfWork;
            _transactionService = transactionService;
            _accountService = accountService;
            _logger = logger;
            _authService = authService;
        }


        public async Task<ApiResponse<String>> Transfer(TransferRequest transferRequest)
        {
            try
            {
                _logger.LogInformation("Initiating transfer from account {SenderAccount} to account {ReceiverAccount} for user {UserId} with amount {Amount}", transferRequest.SenderAccount, transferRequest.ReceiverAccount, transferRequest.UserId, transferRequest.Amount);

                if (transferRequest.Amount <= 0)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Amount must be greater than zero."
                    };
                }

                var isValidAccountResponse = await _accountService.ValidateBankAccount(transferRequest.SenderAccount, transferRequest.ReceiverAccount);
                if (!isValidAccountResponse.Success)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = isValidAccountResponse.Message,
                        Errors = isValidAccountResponse.Errors
                    };
                }

                _logger.LogInformation("Bank accounts validated successfully for sender account {SenderAccount} and receiver account {ReceiverAccount}", transferRequest.SenderAccount, transferRequest.ReceiverAccount);

                var user = await _authService.GetUserById(transferRequest.UserId);
                if (user == null)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Unauthorized access to the sender account."
                    };
                }

                var isTransactionExist = await _transactionService.GetTransactionById(transferRequest.TransactionId);
                if (isTransactionExist.Success)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = "A transaction with the same ID already exists. Duplicate transaction",
                    };
                }
                _logger.LogInformation("No duplicate transaction found for transaction ID {TransactionId}. Proceeding with the transfer.", transferRequest.TransactionId);

                var debitTransactionResponse = await _transactionService.CreateTransaction(transferRequest, TransactionType.Debit, user.UserId);
                if (!debitTransactionResponse.Success)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = debitTransactionResponse.Message,
                        Errors = debitTransactionResponse.Errors
                    };
                }

                var transaction = debitTransactionResponse.Data;

                var debitReponse = await _accountService.DebitAccount(transferRequest.SenderAccount, transferRequest.Amount);
                if (!debitReponse.Success)
                {
                    transaction.Status = TransactionStatus.Failed;
                    await _transactionService.UpdateTransaction(transaction);
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = debitReponse.Message,
                        Errors = debitReponse.Errors
                    };
                }

                _logger.LogInformation("Debit transaction created successfully for user {UserId} with transaction ID {TransactionId}", transferRequest.UserId, debitTransactionResponse.Data.TraansactionId);

                transaction.Status = TransactionStatus.Completed;
                await _transactionService.UpdateTransaction(transaction);


                var creditTransactionResponse = await _transactionService.CreateTransaction(transferRequest, TransactionType.Credit, user.UserId);
                if (!creditTransactionResponse.Success)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = creditTransactionResponse.Message,
                        Errors = creditTransactionResponse.Errors
                    };
                }

                transaction = creditTransactionResponse.Data;

                var creditResponse = await _accountService.CreditAccount(transferRequest.ReceiverAccount, transferRequest.Amount);
                if (!creditResponse.Success)
                {
                    transaction.Status = TransactionStatus.Failed;
                    await _transactionService.UpdateTransaction(transaction);
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = creditResponse.Message,
                        Errors = creditResponse.Errors
                    };
                }

                _logger.LogInformation("Credit transaction created successfully for user {UserId} with transaction ID {TransactionId}", transferRequest.UserId, creditTransactionResponse.Data.TraansactionId);

                transaction.Status = TransactionStatus.Completed;
                await _transactionService.UpdateTransaction(transaction);
                return new ApiResponse<string>
                {
                    Success = true,
                    Message = "Transfer successful.",
                    Data = transaction.Reference
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the transfer.");
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = "An error occurred while processing the transfer. Please try again later."
                };
            }
        }


        public async Task<ApiResponse<string>> CreditAccount(TransferRequest transferRequest)
        {
            try
            {
                _logger.LogInformation("Processing credit transaction for user {UserId} to account {ReceiverAccount} with amount {Amount}", transferRequest.UserId, transferRequest.ReceiverAccount, transferRequest.Amount);

                if (transferRequest.Amount <= 0)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Amount must be greater than zero."
                    };
                }

                var user = await _authService.GetUserById(transferRequest.UserId);
                if (user == null)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Invalid user."
                    };
                }

                var transactionResponse = await _transactionService.CreateTransaction(transferRequest, TransactionType.Credit, user.UserId);
                if (!transactionResponse.Success)
                {
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = transactionResponse.Message,
                        Errors = transactionResponse.Errors
                    };
                }


                var transaction = transactionResponse.Data;
                var creditResponse = await _accountService.CreditAccount(transferRequest.ReceiverAccount, transferRequest.Amount);
                if (!creditResponse.Success)
                {
                    transaction.Status = TransactionStatus.Failed;
                    await _transactionService.UpdateTransaction(transaction);
                    return new ApiResponse<string>
                    {
                        Success = false,
                        Message = creditResponse.Message,
                        Errors = creditResponse.Errors
                    };
                }

                _logger.LogInformation("Credit transaction created successfully for user {UserId} with transaction ID {TransactionId}", transferRequest.UserId, transactionResponse.Data.TraansactionId);


                transaction.Status = TransactionStatus.Completed;
                await _transactionService.UpdateTransaction(transaction);
                return new ApiResponse<string>
                {
                    Success = true,
                    Message = "Account credited successfully.",
                    Data = transaction.Reference
                };
            }
            catch (Exception)
            {

                _logger.LogError("An error occurred while processing the credit transaction.");
                return new ApiResponse<string>
                {
                    Success = false,
                    Message = "An error occurred while processing the credit transaction. Please try again later."
                };
            }
        }
    }
}
