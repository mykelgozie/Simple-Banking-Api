

namespace Bank.Application.Interface
{
    public interface IUnitOfWork
    {
        public IAccountRepository AccountRepository { get; }
        ITransactionRepository TransactionRepository { get; }

        void Dispose();
        Task RollBackAsync();
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
