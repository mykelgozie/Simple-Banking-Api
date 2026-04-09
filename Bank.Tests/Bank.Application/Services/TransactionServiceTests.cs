using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bank.Application.Interface;
using Bank.Application.Services;
using Bank.Domain.Dtos.Request;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Bank.Tests
{
    public class TransactionServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<ITransactionRepository> _txRepoMock;
        private readonly Mock<ILogger<TransactionService>> _loggerMock;
        private readonly TransactionService _service;

        public TransactionServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _txRepoMock = new Mock<ITransactionRepository>();
            _loggerMock = new Mock<ILogger<TransactionService>>();

            _uowMock.Setup(u => u.TransactionRepository).Returns(_txRepoMock.Object);
            _uowMock.Setup(u => u.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _uowMock.Setup(u => u.RollBackAsync()).Returns(Task.CompletedTask);

            _service = new TransactionService(_uowMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task CreateTransaction_CreatesDebitTransaction_OnSuccess()
        {
            TransferRequest req = new TransferRequest
            {
                SenderAccount = "S-123",
                ReceiverAccount = "R-456",
                Amount = 150.75m,
                TransactionId = "T-1"
            };

            Transaction captured = null;
            _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>()))
                .Callback<Transaction>(t => captured = t)
                .Returns(Task.CompletedTask);

            var res = await _service.CreateTransaction(req, TransactionType.Debit, "user-42");

            Assert.True(res.Success);
            Assert.NotNull(res.Data);
            Assert.Equal("S-123", res.Data.AccountNumber);
            Assert.Equal(req.Amount, res.Data.Amount);
            Assert.Equal(TransactionType.Debit, res.Data.TransactionType);
            Assert.Equal(TransactionStatus.Pending, res.Data.Status);
            Assert.Equal("T-1", res.Data.TraansactionId);
            Assert.Equal("user-42", res.Data.userId);
            Assert.False(string.IsNullOrWhiteSpace(res.Data.Reference));
            Assert.Equal(11, res.Data.Reference.Length); // default length
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
            // ensure the captured entity is the same as returned
            Assert.Equal(res.Data.AccountNumber, captured.AccountNumber);
        }

        [Fact]
        public async Task CreateTransaction_CreatesCreditTransaction_UsesReceiverAccount()
        {
            var req = new TransferRequest
            {
                SenderAccount = "S-AA",
                ReceiverAccount = "R-BB",
                Amount = 10m,
                TransactionId = "TX-CR"
            };

            Transaction captured = null;
            _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>()))
                .Callback<Transaction>(t => captured = t)
                .Returns(Task.CompletedTask);

            var res = await _service.CreateTransaction(req, TransactionType.Credit, "uid");

            Assert.True(res.Success);
            Assert.Equal("R-BB", res.Data.AccountNumber);
            Assert.Equal(req.Amount, res.Data.Amount);
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateTransaction_ReturnsFail_AndRollsBack_OnException()
        {
            var req = new TransferRequest
            {
                SenderAccount = "S-X",
                ReceiverAccount = "R-Y",
                Amount = 1m,
                TransactionId = "E-1"
            };

            _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>())).ThrowsAsync(new Exception("db error"));

            var res = await _service.CreateTransaction(req, TransactionType.Debit, "u");

            Assert.False(res.Success);
            _uowMock.Verify(u => u.RollBackAsync(), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsTransaction_WhenFound()
        {
            var tx = new Transaction
            {
                AccountNumber = "A1",
                TraansactionId = "find-me",
                Amount = 9.9m
            };

            _txRepoMock.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>()))
                .ReturnsAsync(tx);

            var res = await _service.GetTransactionById("find-me");

            Assert.True(res.Success);
            Assert.NotNull(res.Data);
            Assert.Equal("find-me", res.Data.TraansactionId);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsFail_WhenNotFound()
        {
            _txRepoMock.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>()))
                .ReturnsAsync((Transaction)null);

            var res = await _service.GetTransactionById("missing");

            Assert.False(res.Success);
            Assert.Contains("not found", res.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(res.Data);
        }

        [Fact]
        public async Task GetTransactionById_ReturnsFail_OnException()
        {
            _txRepoMock.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Transaction, bool>>>()))
                .ThrowsAsync(new Exception("boom"));

            var res = await _service.GetTransactionById("err");

            Assert.False(res.Success);
            Assert.Null(res.Data);
        }

        [Fact]
        public async Task UpdateTransaction_UpdatesAndSaves_OnSuccess()
        {
            var tx = new Transaction
            {
                TraansactionId = "U-1",
                Status = TransactionStatus.Pending
            };

            _txRepoMock.Setup(r => r.Update(It.IsAny<Transaction>()));

            var res = await _service.UpdateTransaction(tx);

            Assert.True(res.Success);
            Assert.Equal(tx, res.Data);
            _txRepoMock.Verify(r => r.Update(It.Is<Transaction>(t => t.TraansactionId == "U-1")), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateTransaction_ReturnsFail_AndRollsBack_OnException()
        {
            var tx = new Transaction { TraansactionId = "U-2" };

            _txRepoMock.Setup(r => r.Update(It.IsAny<Transaction>())).Throws(new Exception("update failed"));

            var res = await _service.UpdateTransaction(tx);

            Assert.False(res.Success);
            _uowMock.Verify(u => u.RollBackAsync(), Times.Once);
        }

        [Fact]
        public void GenerateTransactionReference_PrivateMethod_ReturnsCustomLength()
        {
            // use reflection to call private method with custom length
            var mi = typeof(TransactionService).GetMethod("GenerateTransactionReference", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(mi);

            var result = mi.Invoke(_service, new object[] { 5 }) as string;
            Assert.NotNull(result);
            Assert.Equal(5, result.Length);

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            Assert.True(result.All(c => chars.Contains(c)));
        }
    }
}