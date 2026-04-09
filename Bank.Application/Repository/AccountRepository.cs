using Bank.Application.Interface;
using Bank.Domain.Entities;
using Bank.Infrastructure.Persistence;
using Bank.Infrastructure.Repository;

namespace Bank.Application.Repository
{
    public class AccountRepository : GenericRepository<Account>, IAccountRepository
    {
        private AppDbContext _appDbContext;

        public AccountRepository(AppDbContext appDbContext) :base(appDbContext)
        {
            _appDbContext = appDbContext;
        }


    }
}
