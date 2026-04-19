using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bank.Application.Services;
using Bank.Application.Interface;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Dtos.Response;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bank.Tests
{
    public class FinacialServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<ITransactionService> _transactionServiceMock;
        private readonly Mock<IAccountService> _accountServiceMock;
        private readonly Mock<ILogger<FinacialService>> _loggerMock;
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<IKafkaProducer> _kafkaProducerMock;
        private readonly FinacialService _service;

        public FinacialServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _transactionServiceMock = new Mock<ITransactionService>();
            _accountServiceMock = new Mock<IAccountService>();
            _loggerMock = new Mock<ILogger<FinacialService>>();
            _authServiceMock = new Mock<IAuthService>();
            _kafkaProducerMock = new Mock<IKafkaProducer>();
            _service = new FinacialService(
                _uowMock.Object,
                _transactionServiceMock.Object,
                _accountServiceMock.Object,
                _loggerMock.Object,
                _authServiceMock.Object,
                _kafkaProducerMock.Object
            );
        }

        private static TransferRequest CreateRequest(decimal amount = 10m)
        {
            return new TransferRequest
            {
                SenderAccount = "S-123",
                ReceiverAccount = "R-456",
                Amount = amount,
                TransactionId = Guid.NewGuid().ToString(),
                UserId = "user-1"
            };
        }

        private static RegisterUserResponse CreateUser() =>
            new RegisterUserResponse { UserId = "user-1", Email = "a@b.com", FirstName = "F", LastName = "L" };

        private static Transaction CreateTransaction(string reference = "ref", string traId = "tid") =>
            new Transaction
            {
                AccountNumber = "S-123",
                Amount = 10m,
                Reference = reference,
                TraansactionId = traId,
                Status = TransactionStatus.Pending,
                TransactionType = TransactionType.Debit,
                userId = "user-1"
            };

        [Fact]
        public async Task Transfer_ReturnsFail_WhenAmountLessOrEqualZero()
        {
            var req = CreateRequest(0m);

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Contains("greater than zero", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_WhenValidateBankAccountFails()
        {
            var req = CreateRequest();
            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Fail("invalid accounts", new List<string> { "err" }));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Equal("invalid accounts", res.Message);
            Assert.NotNull(res.Errors);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_WhenUserIsNull()
        {
            var req = CreateRequest();
            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync((RegisterUserResponse)null);

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Contains("Unauthorized", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_WhenDuplicateTransactionExists()
        {
            var req = CreateRequest();
            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(CreateTransaction()));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Contains("already exists", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_WhenDebitTransactionCreationFails()
        {
            var req = CreateRequest();
            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("not found"));
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Debit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("cannot create"));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Equal("cannot create", res.Message);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_AndUpdatesTransaction_WhenDebitAccountFails()
        {
            var req = CreateRequest();
            var createdTx = CreateTransaction();

            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));

        _auth_service_setup:
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());

            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("not found"));

            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Debit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(createdTx));

            _accountServiceMock
                .Setup(a => a.DebitAccount(req.SenderAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Fail("insufficient"));

            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(createdTx));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Equal("insufficient", res.Message);
            _transactionServiceMock.Verify(t => t.UpdateTransaction(It.Is<Transaction>(tr => tr.Status == TransactionStatus.Failed)), Times.Once);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_WhenCreditTransactionCreationFails()
        {
            var req = CreateRequest();
            var debitTx = CreateTransaction("debitRef", "d1");

            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("not found"));
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Debit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(debitTx));
            _accountServiceMock
                .Setup(a => a.DebitAccount(req.SenderAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Ok(true));
            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(debitTx));
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Credit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("credit create failed"));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Equal("credit create failed", res.Message);
        }

        [Fact]
        public async Task Transfer_ReturnsFail_AndUpdatesTransaction_WhenCreditAccountFails()
        {
            var req = CreateRequest();
            var debitTx = CreateTransaction("debitRef", "d1");
            var creditTx = CreateTransaction("creditRef", "c1");

            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("not found"));
            _transactionServiceMock
                .SetupSequence(t => t.CreateTransaction(req, It.IsAny<TransactionType>(), It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(debitTx))   // first call debit
                .ReturnsAsync(ApiResponse<Transaction>.Ok(creditTx)); // second call credit
            _accountServiceMock
                .Setup(a => a.DebitAccount(req.SenderAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Ok(true));
            _accountServiceMock
                .Setup(a => a.CreditAccount(req.ReceiverAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Fail("credit failed"));
            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(creditTx));

            var res = await _service.Transfer(req);

            Assert.False(res.Success);
            Assert.Equal("credit failed", res.Message);
            _transactionServiceMock.Verify(t => t.UpdateTransaction(It.Is<Transaction>(tr => tr.Status == TransactionStatus.Failed)), Times.Once);
        }

        [Fact]
        public async Task Transfer_ReturnsSuccess_OnCompleteTransfer()
        {
            var req = CreateRequest();
            var debitTx = CreateTransaction("debitRef", "d1");
            var creditTx = CreateTransaction("creditRef", "c1");

            _accountServiceMock
                .Setup(a => a.ValidateBankAccount(req.SenderAccount, req.ReceiverAccount))
                .ReturnsAsync(ApiResponse<string>.Ok("Accounts are valid"));
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.GetTransactionById(req.TransactionId))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("not found"));
            _transactionServiceMock
                .SetupSequence(t => t.CreateTransaction(req, It.IsAny<TransactionType>(), It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(debitTx))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(creditTx));
            _accountServiceMock
                .Setup(a => a.DebitAccount(req.SenderAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Ok(true));
            _accountServiceMock
                .Setup(a => a.CreditAccount(req.ReceiverAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Ok(true));
            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => ApiResponse<Transaction>.Ok(t));

            var res = await _service.Transfer(req);

            Assert.True(res.Success);
            Assert.Equal(creditTx.Reference, res.Data);
        }

        // =========================
        // Tests for CreditAccount
        // =========================

        [Fact]
        public async Task CreditAccount_ReturnsFail_WhenAmountLessOrEqualZero()
        {
            var req = CreateRequest(0m);

            var res = await _service.CreditAccount(req);

            Assert.False(res.Success);
            Assert.Contains("greater than zero", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreditAccount_ReturnsFail_WhenUserIsNull()
        {
            var req = CreateRequest();
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync((RegisterUserResponse)null);

            var res = await _service.CreditAccount(req);

            Assert.False(res.Success);
            Assert.Contains("Invalid user", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreditAccount_ReturnsFail_WhenCreateTransactionFails()
        {
            var req = CreateRequest();
            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Credit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Fail("create failed"));

            var res = await _service.CreditAccount(req);

            Assert.False(res.Success);
            Assert.Equal("create failed", res.Message);
        }

        [Fact]
        public async Task CreditAccount_ReturnsFail_AndUpdatesTransaction_WhenCreditAccountFails()
        {
            var req = CreateRequest();
            var tx = CreateTransaction("cRef", "c1");

            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Credit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(tx));
            _accountServiceMock
                .Setup(a => a.CreditAccount(req.ReceiverAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Fail("credit failed"));
            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(tx));

            var res = await _service.CreditAccount(req);

            Assert.False(res.Success);
            Assert.Equal("credit failed", res.Message);
            _transactionServiceMock.Verify(t => t.UpdateTransaction(It.Is<Transaction>(tr => tr.Status == TransactionStatus.Failed)), Times.Once);
        }

        [Fact]
        public async Task CreditAccount_ReturnsSuccess_OnSuccess()
        {
            var req = CreateRequest();
            var tx = CreateTransaction("cRef", "c1");

            _authServiceMock
                .Setup(a => a.GetUserById(req.UserId))
                .ReturnsAsync(CreateUser());
            _transactionServiceMock
                .Setup(t => t.CreateTransaction(req, TransactionType.Credit, It.IsAny<string>()))
                .ReturnsAsync(ApiResponse<Transaction>.Ok(tx));
            _accountServiceMock
                .Setup(a => a.CreditAccount(req.ReceiverAccount, req.Amount))
                .ReturnsAsync(ApiResponse<bool>.Ok(true));
            _transactionServiceMock
                .Setup(t => t.UpdateTransaction(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => ApiResponse<Transaction>.Ok(t));

            var res = await _service.CreditAccount(req);

            Assert.True(res.Success);
            Assert.Equal(tx.Reference, res.Data);
        }
    }
}