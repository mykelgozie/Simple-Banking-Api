using Bank.Application.Interface;
using Bank.Application.Services;
using Bank.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bank.Tests
{
    public class AccountServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<IAccountRepository> _accountRepoMock;
        private readonly Mock<ILogger<AccountService>> _loggerMock;
        private readonly AccountService _service;

        public AccountServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _accountRepoMock = new Mock<IAccountRepository>();
            _loggerMock = new Mock<ILogger<AccountService>>();

            _uowMock.Setup(u => u.AccountRepository).Returns(_accountRepoMock.Object);
            _uowMock.Setup(u => u.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _uowMock.Setup(u => u.RollBackAsync()).Returns(Task.CompletedTask);

            _service = new AccountService(_uowMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task CreateAccountAsync_ReturnsFail_WhenUserIdIsNullOrEmpty()
        {
            var resNull = await _service.CreateAccountAsync(null);
            Assert.False(resNull.Success);
            Assert.Contains("cannot be null", resNull.Message, StringComparison.OrdinalIgnoreCase);

            var resEmpty = await _service.CreateAccountAsync(string.Empty);
            Assert.False(resEmpty.Success);
            Assert.Contains("cannot be null", resEmpty.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateAccountAsync_ReturnsFail_WhenAccountAlreadyExists()
        {
            var existing = new Account { UserId = "user-1" };
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(existing);

            var res = await _service.CreateAccountAsync("user-1");

            Assert.False(res.Success);
            Assert.Contains("already exists", res.Message, StringComparison.OrdinalIgnoreCase);
            _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<Account>()), Times.Never);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAccountAsync_CreatesAccount_OnSuccess()
        {
            _accountRepoMock
                .SetupSequence(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync((Account)null); // ensure no existing account

            Account captured = null;
            _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<Account>()))
                .Callback<Account>(a => captured = a)
                .Returns(Task.CompletedTask);

            var res = await _service.CreateAccountAsync("user-2");

            Assert.True(res.Success);
            Assert.NotNull(res.Data);
            Assert.Equal("user-2", res.Data.UserId);
            Assert.Equal(0m, res.Data.Balance);
            Assert.NotNull(res.Data.AccountNumber);
            _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<Account>()), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
            // ensure the captured entity matches the returned data
            Assert.Equal(res.Data.AccountNumber, captured.AccountNumber);
        }

        [Fact]
        public async Task DebitAccount_ReturnsFail_WhenAccountNotFound()
        {
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync((Account)null);

            var res = await _service.DebitAccount("nonexistent", 10m);

            Assert.False(res.Success);
            _uowMock.Verify(u => u.RollBackAsync(), Times.Once);
        }

        [Fact]
        public async Task DebitAccount_ReturnsFail_WhenInsufficientFunds()
        {
            var acct = new Account { AccountNumber = "A1", Balance = 5m };
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(acct);

            var res = await _service.DebitAccount("A1", 10m);

            Assert.False(res.Success);
            _uowMock.Verify(u => u.RollBackAsync(), Times.Once);
        }

        [Fact]
        public async Task DebitAccount_DebitsBalance_OnSuccess()
        {
            var acct = new Account { AccountNumber = "A2", Balance = 100m };
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(acct);

            _accountRepoMock.Setup(r => r.Update(It.IsAny<Account>()));

            var res = await _service.DebitAccount("A2", 25m);

            Assert.True(res.Success);
            Assert.Equal(75m, acct.Balance);
            _accountRepoMock.Verify(r => r.Update(It.Is<Account>(a => a.AccountNumber == "A2" && a.Balance == 75m)), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreditAccount_ReturnsFail_WhenAccountNotFound()
        {
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync((Account)null);

            var res = await _service.CreditAccount("nope", 50m);

            Assert.False(res.Success);
            _uowMock.Verify(u => u.RollBackAsync(), Times.Once);
        }

        [Fact]
        public async Task CreditAccount_IncreasesBalance_OnSuccess()
        {
            var acct = new Account { AccountNumber = "C1", Balance = 10m };
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(acct);

            _accountRepoMock.Setup(r => r.Update(It.IsAny<Account>()));

            var res = await _service.CreditAccount("C1", 40m);

            Assert.True(res.Success);
            Assert.Equal(50m, acct.Balance);
            _accountRepoMock.Verify(r => r.Update(It.Is<Account>(a => a.AccountNumber == "C1" && a.Balance == 50m)), Times.Once);
            _uowMock.Verify(u => u.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAccountByUserIdAsync_ReturnsFail_WhenNotFound()
        {
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync((Account)null);

            var res = await _service.GetAccountByUserIdAsync("missing-user");

            Assert.False(res.Success);
            Assert.Contains("not found", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetAccountByUserIdAsync_ReturnsAccount_OnSuccess()
        {
            var acct = new Account { UserId = "u-3", AccountNumber = "AN3" };
            _accountRepoMock
                .Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(acct);

            var res = await _service.GetAccountByUserIdAsync("u-3");

            Assert.True(res.Success);
            Assert.Equal("u-3", res.Data.UserId);
        }

        [Fact]
        public async Task ValidateBankAccount_ReturnsFail_WhenSenderMissing()
        {
            // sender missing
            _accountRepoMock
                .SetupSequence(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync((Account)null)   // sender
                .ReturnsAsync(new Account());  // receiver (should not matter)

            var res = await _service.ValidateBankAccount("S1", "R1");

            Assert.False(res.Success);
            Assert.Contains("not found", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateBankAccount_ReturnsFail_WhenReceiverMissing()
        {
            // sender exists, receiver missing
            _accountRepoMock
                .SetupSequence(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(new Account())   // sender
                .ReturnsAsync((Account)null);  // receiver

            var res = await _service.ValidateBankAccount("S2", "R2");

            Assert.False(res.Success);
            Assert.Contains("not found", res.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ValidateBankAccount_ReturnsOk_WhenBothExist()
        {
            _accountRepoMock
                .SetupSequence(r => r.GetFirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Account, bool>>>()))
                .ReturnsAsync(new Account { AccountNumber = "S3" })   // sender
                .ReturnsAsync(new Account { AccountNumber = "R3" });  // receiver

            var res = await _service.ValidateBankAccount("S3", "R3");

            Assert.True(res.Success);
            Assert.Equal("Accounts are valid", res.Data);
        }
    }
}