using BankingAppCore.Data;
using BankingAppCore.Models;

namespace BankingAppCore.Services
{
    public class BankAccountService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<BankAccountService> _logger;

        public BankAccountService(ApplicationDbContext context, ILogger<BankAccountService> logger)
        {
            _dbContext = context;
            _logger = logger;
        }

        public async Task AddInterestToSavingsAccountsAsync()
        {
            var savingsAccounts = await _dbContext.GetSavingsAccountsListAsync();
            var interestPercentage = 0.05m;
            var transactionRecords = new List<TransactionRecord>();

            foreach (var account in savingsAccounts)
            {
                var interestAmount = account.Balance * interestPercentage;

                transactionRecords.Add(new TransactionRecord()
                {
                    Sender = account.AccountID,
                    Recipient = account.AccountID,
                    Amount = interestAmount,
                    Status = TransactionStatus.Approved,
                    TransactionType = TransactionType.Interest,
                    Description = "Weekly percentage-based bonus for the savings account",
                    TimeExecuted = DateTime.Now
                });

                account.Balance += interestAmount;
            }

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    _dbContext.AddRange(transactionRecords);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError($"Error adding interest to the savings accounts. {ex.Message}", ex);
                }
            }
        }
    }
}
