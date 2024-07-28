using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BankingAppCore.Models;
using Microsoft.AspNetCore.Identity;

namespace BankingAppCore.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public DbSet<Card> Cards { get; set; }
        public DbSet<TransactionRecord> TransactionRecords { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Customize the table names by using EF's Fluent API.
            modelBuilder.Entity<User>().Property(u => u.Id).HasColumnName("UserID");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<Card>().HasIndex(c => c.CardNumber).IsUnique();

            
            modelBuilder.Entity<TransactionRecord>(entity =>
            {
                // Prevent cascade delete on multi-pathed entities for both related accounts
                entity.HasOne(tr => tr.SenderAccount)
                    .WithMany()
                    .HasForeignKey(tr => tr.Sender)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(tr => tr.RecipientAccount)
                    .WithMany()
                    .HasForeignKey(tr => tr.Recipient)
                    .OnDelete(DeleteBehavior.Restrict);

                // Certificates don't have to be suppled (only for store transactions
                entity.Property(tr => tr.Certificate)
                    .IsRequired(false);
            });

            modelBuilder.Entity<BankAccount>()
                .Property(ba => ba.PermissionKey)
                .IsRequired(false);

            // Enabling multiple constraint on User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email)
                    .IsUnique();

                entity.Property(e => e.Alias)
                    .IsRequired(false);

                entity.Property(e => e.FirstName)
                    .IsRequired(false);

                entity.Property(e => e.LastName)
                    .IsRequired(false);
            });
        }

        public enum AccountAttribute
        {
            Type,
            Alias
        }

        public IQueryable<User> GetUserQuery(int? userID)
        {
            return Users.Where(u => u.Id == userID);
        }

        public bool CheckExistingUser(string alias)
        {
            User existingUser = Users.Where(u => u.Alias == alias).FirstOrDefault();
            return existingUser != null;
        }

        public async Task<bool> CheckExistingUserAsync(string alias)
        {
            User existingUser = await Users.Where(u => u.Alias == alias).FirstOrDefaultAsync();
            return existingUser != null;
        }

        public List<BankAccount> GetBankAccountsList(int? userID)
        {
            return BankAccounts.Where(ba => ba.Holder == userID).ToList();
        }

        public async Task<List<BankAccount>> GetActiveBankAccountsListAsync(int? userID)
        {
            return await BankAccounts.Where(ba => ba.Holder == userID && ba.Active).ToListAsync();
        }

        public BankAccount GetBankAccountFromPKey(int accountID)
        {
            return BankAccounts.Where(ba => ba.AccountID == accountID).FirstOrDefault();
        }

        public async Task<BankAccount> GetBankAccountFromPKeyAsync(int accountID)
        {
            return await BankAccounts.Where(ba => ba.AccountID == accountID).FirstOrDefaultAsync();
        }

        public DateTime GetLastCreatedBankAccountTimestamp(int? userID)
        {
            return BankAccounts.Where(ba => ba.Holder == userID).OrderByDescending(ba => ba.DateOpened).Select(ba => ba.DateOpened).FirstOrDefault();
        }

        public async Task<DateTime> GetLastCreatedBankAccountTimestampAsync(int? userID)
        {
            return await BankAccounts.Where(ba => ba.Holder == userID).OrderByDescending(ba => ba.DateOpened).Select(ba => ba.DateOpened).FirstOrDefaultAsync();
        }

        public object GetAccountAttribute(int? userID, AccountAttribute attribute)
        {
            var userQuery = GetUserQuery(userID);

            if (attribute == AccountAttribute.Type)
            {
                return userQuery.Select(u => u.UserType).FirstOrDefault();
            }
            else if (attribute == AccountAttribute.Alias)
            {
                return userQuery.Select(u => u.Alias).FirstOrDefault();
            }
            else
            {
                throw new ArgumentException("Invalid format specified", nameof(attribute));
            }
        }

        public async Task<object> GetAccountAttributeAsync(int? userID, AccountAttribute attribute)
        {
            var userAccountQuery = GetUserQuery(userID);

            if (attribute == AccountAttribute.Type)
            {
                return await userAccountQuery.Select(u => u.UserType).FirstOrDefaultAsync();
            }
            else if (attribute == AccountAttribute.Alias)
            {
                return await userAccountQuery.Select(u => u.Alias).FirstOrDefaultAsync();
            }
            else
            {
                throw new ArgumentException("Invalid format specified", nameof(attribute));
            }
        }

        public BankAccount GetBankAccountFromAlias(string alias)
        {
            return BankAccounts.Where(ba => ba.User.Alias == alias)
                .Include(ba => ba.User)
                .FirstOrDefault();
        }

        public async Task<BankAccount> GetBankAccountFromAliasAsync(string alias)
        {
            return await BankAccounts.Where(ba => ba.User.Alias == alias)
                .Include(ba => ba.User)
                .FirstOrDefaultAsync();
        }

        public List<BankAccount> GetSavingsAccountsList()
        {
            return BankAccounts.Where(ba => ba.AccountType == AccountType.Savings && ba.Active)
                .Include(ba => ba.User)
                .ToList();
        }

        public async Task<List<BankAccount>> GetSavingsAccountsListAsync()
        {
            return await BankAccounts.Where(ba => ba.AccountType == AccountType.Savings && ba.Active)
                .Include(ba => ba.User)
                .ToListAsync();
        }

        public Card GetCardFromPKey(int cardID)
        {
            return Cards.Where(c => c.CardID == cardID).FirstOrDefault();
        }

        public async Task<Card> GetCardFromPKeyAsync(int cardID)
        {
            return await Cards.Where(c => c.CardID == cardID).FirstOrDefaultAsync();
        }

        public Card GetCardFromCardNumber(string cardNumber)
        {
            return Cards.Where(c => c.CardNumber == cardNumber)
                .Include(c => c.AssociatedBankAccount)
                .FirstOrDefault();
        }

        public async Task<Card> GetCardFromFromCardNumberAsync(string cardNumber)
        {
            return await Cards.Where(c => c.CardNumber == cardNumber)
                .Include(c => c.AssociatedBankAccount)
                .FirstOrDefaultAsync();
        }

        public List<Card> GetActiveCardsList(int? userID)
        {
            return Cards.Where(c => c.AssociatedBankAccount.Holder == userID && c.Active).ToList();
        }

        public async Task<List<Card>> GetActiveCardsListAsync(int? userID)
        {
            return await Cards.Where(c => c.AssociatedBankAccount.Holder == userID && c.Active).ToListAsync();
        }

        public bool CheckExistingCard(string cardNumber)
        {
            Card card = Cards.Where(c => c.CardNumber == cardNumber).FirstOrDefault();
            return card != null;
        }

        public async Task<bool> CheckExistingCardAsync(string cardNumber)
        {
            Card card = await Cards.Where(c => c.CardNumber == cardNumber).FirstOrDefaultAsync();
            return card != null;
        }

        public DateTime GetLastCreatedCardTimestamp(int? userID)
        {
            return Cards.Where(c => c.AssociatedBankAccount.Holder == userID).OrderByDescending(c => c.IssueDate).Select(c => c.IssueDate).FirstOrDefault();
        }

        public async Task<DateTime> GetLastCreatedCardTimestampAsync(int? userID)
        {
            return await Cards.Where(c => c.AssociatedBankAccount.Holder == userID).OrderByDescending(c => c.IssueDate).Select(c => c.IssueDate).FirstOrDefaultAsync();
        }

        public List<Card> GetRelatedCardsFromBankAccount(int? bankAccountID)
        {
            return Cards.Where(c => c.AssociatedAccount == bankAccountID).ToList();
        }

        public async Task<List<Card>> GetRelatedCardsFromBankAccountAsync(int? bankAccountID)
        {
            return await Cards.Where(c => c.AssociatedAccount == bankAccountID).ToListAsync();
        }

        public List<TransactionRecord> GetTransactionRecordsList(int? userID)
        {
            return TransactionRecords.Where(tr => tr.SenderAccount.Holder == userID || tr.RecipientAccount.Holder == userID)
                .Include(tr => tr.SenderAccount)
                .ThenInclude(sa => sa.User)
                .Include(tr => tr.RecipientAccount)
                .ThenInclude(rc => rc.User)
                .ToList();
        }

        public Task<List<TransactionRecord>> GetTransactionRecordsListAsync(int? userID)
        {
            return TransactionRecords.Where(tr => tr.SenderAccount.Holder == userID || tr.RecipientAccount.Holder == userID)
                .Include(tr => tr.SenderAccount)
                .ThenInclude(sa => sa.User)
                .Include(tr => tr.RecipientAccount)
                .ThenInclude(rc => rc.User)
                .ToListAsync();
        }

        public TransactionRecord GetTransactionRecordFromCertificate(string certificate)
        {
            return TransactionRecords.Where(tr => tr.Certificate == certificate)
                .Include(tr => tr.SenderAccount)
                .ThenInclude(sa => sa.User)
                .Include(tr => tr.RecipientAccount)
                .ThenInclude(ra => ra.User)
                .FirstOrDefault();
        }

        public async Task<TransactionRecord> GetTransactionRecordAsyncFromCertificate(string certificate)
        {
            return await TransactionRecords.Where(tr => tr.Certificate == certificate)
                .Include(tr => tr.SenderAccount)
                .ThenInclude(sa => sa.User)
                .Include(tr => tr.RecipientAccount)
                .ThenInclude(ra => ra.User)
                .FirstOrDefaultAsync();
        }

        public List<Card> GetActiveExpiredCardsList(DateTime currentTime)
        {
            return Cards.Where(c => c.ExpireDate <= currentTime && c.Active).ToList();
        }

        public async Task<List<Card>> GetActiveExpiredCardsListAsync(DateTime currentTime)
        {
            return await Cards.Where(c => c.ExpireDate <= currentTime && c.Active).ToListAsync();
        }
    }
}
