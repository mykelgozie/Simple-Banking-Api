using Bank.Application.Interface;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Bank.Application.Services
{
    public class AccountService : IAccountService
    {
        private IUnitOfWork _unitOfWork;
        private ILogger<AccountService> _logger;

        public AccountService(IUnitOfWork unitOfWork, ILogger<AccountService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ApiResponse<Account>> CreateAccountAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ApiResponse<Account>.Fail("User ID cannot be null or empty");
                }

                var account = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.UserId == userId);
                if (account != null)
                {
                    return ApiResponse<Account>.Fail("Account already exists for this user");
                }

                account = new Account
                {
                    UserId = userId,
                    Balance = 0,
                    CurrencyCode = "NGN",
                    AccountNumber = GenerateAccountNumber(),
                };

                await _unitOfWork.AccountRepository.AddAsync(account);
                await _unitOfWork.SaveAsync();
                return ApiResponse<Account>.Ok(account, "Account created successfully");
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollBackAsync();
                return ApiResponse<Account>.Fail("Failed to create account", new List<string> { ex.Message });
            }
        }

        private string GenerateAccountNumber()
        {
            return RandomNumberGenerator.GetInt32(1000000000, int.MaxValue)
              .ToString()
              .PadLeft(11, '0')
              .Substring(0, 11);
        }


        public async Task<ApiResponse<bool>> DebitAccount(string senderAccountNumber, decimal amount)
        {
            try
            {
                var account = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.AccountNumber == senderAccountNumber);
                if (account == null)
                {
                    throw new Exception("Account not found");
                }
                if (account.Balance < amount)
                {
                    throw new Exception("Insufficient funds");
                }
                account.Balance -= amount;
                _unitOfWork.AccountRepository.Update(account);
                await _unitOfWork.SaveAsync();

                return ApiResponse<bool>.Ok(true, "Account debited successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error debiting account {senderAccountNumber}: {ex.Message}");
                await _unitOfWork.RollBackAsync();
                return ApiResponse<bool>.Fail("Failed to debit account");
            }
        }

        public async Task<ApiResponse<bool>> CreditAccount(string recieverAccountNumber, decimal amount)
        {
            try
            {
                var account = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.AccountNumber == recieverAccountNumber);
                if (account == null)
                {
                    throw new Exception("Account not found");
                }
                account.Balance += amount;
                _unitOfWork.AccountRepository.Update(account);
                await _unitOfWork.SaveAsync();
                return ApiResponse<bool>.Ok(true, "Account credited successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error crediting account {recieverAccountNumber}: {ex.Message}");
                await _unitOfWork.RollBackAsync();
                return ApiResponse<bool>.Fail("Failed to credit account");
            }
        }

        public async Task<ApiResponse<Account>> GetAccountByUserIdAsync(string userId)
        {
            try
            {
                var account = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.UserId == userId);
                if (account == null)
                {
                    return ApiResponse<Account>.Fail("Account not found for the given user ID");
                }
                return ApiResponse<Account>.Ok(account, "Account retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving account for user ID {userId}: {ex.Message}");
                return ApiResponse<Account>.Fail("Failed to retrieve account", new List<string> { ex.Message });
            }
        }


        public async Task<ApiResponse<string>> ValidateBankAccount(string senderAcountNumber, string recieverAccountNumber)
        {
            try
            {

                var senderAccount = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.AccountNumber == senderAcountNumber);
                if (senderAccount == null)
                {
                    return ApiResponse<string>.Fail($"Sender account {senderAccount} not found");
                }
                var recieverAccount = await _unitOfWork.AccountRepository.GetFirstOrDefaultAsync(a => a.AccountNumber == recieverAccountNumber);
                if (recieverAccount == null)
                {
                    return ApiResponse<string>.Fail($"Reciever account {recieverAccountNumber} not found");
                }

                return ApiResponse<string>.Ok("Accounts are valid", "Accounts validated successfully");
            }
            catch (Exception)
            {
                _logger.LogError($"Error validating accounts: Sender - {senderAcountNumber}, Receiver - {recieverAccountNumber}");
                return ApiResponse<string>.Fail("Failed to validate accounts");
            }

        }

    }
}