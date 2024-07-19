using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.SqlServer.Server;

namespace BankingApp.Models
{
    public class ApplicationDbContext : IdentityDbContext<User, CustomRole, int, CustomUserLogin, CustomUserRole, CustomUserClaim>
    {
        public DbSet<Card> Cards { get; set; }
        public DbSet<TransactionRecord> TransactionRecords { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }

        public ApplicationDbContext()
            : base("DefaultConnection")     // "DefaultConnection" is the default configuration.
        {
        }

        public enum AccountAttribute
        {
            Type,
            Alias
        }

        public IQueryable<User> GetUserQuery(int userID)
        {
            return Users
                .Where(u => u.Id == userID);
        }

        public bool CheckExistingUser(string alias)
        {
            User existingUser = Users
                .Where(u => u.Alias == alias)
                .FirstOrDefault();

            return (existingUser != null) ? true : false;
        }

        public async Task<bool> CheckExistingUserAsync(string alias)
        {
            User existingUser = await Users
                .Where(u => u.Alias == alias)
                .FirstOrDefaultAsync();

            return (existingUser != null) ? true : false;
        }

        public List<BankAccount> GetBankAccountsList(int userID)
        {
            return BankAccounts
                .Where(ba => ba.Holder == userID)
                .ToList();
        }

        public async Task<List<BankAccount>> GetBankAccountsListAsync(int userID)
        {
            return await BankAccounts
                .Where(ba => ba.Holder == userID)
                .ToListAsync();
        }

        public BankAccount GetBankAccountFromPKey(int accountID)
        {
            return BankAccounts
                .Where(ba => ba.AccountID == accountID)
                .FirstOrDefault();
        }

        public async Task<BankAccount> GetBankAccountFromPKeyAsync(int accountID)
        {
            return await BankAccounts
                .Where(ba => ba.AccountID == accountID)
                .FirstOrDefaultAsync();
        }

        public DateTime GetLastCreatedBankAccountTimestamp(int userID)
        {
            return BankAccounts
                .Where(ba => ba.Holder == userID)
                .OrderByDescending(ba => ba.DateOpened)
                .Select(ba => ba.DateOpened)
                .FirstOrDefault();
        }

        public async Task<DateTime> GetLastCreatedBankAccountTimestampAsync(int userID)
        {
            return await BankAccounts
                .Where(ba => ba.Holder == userID)
                .OrderByDescending(ba => ba.DateOpened)
                .Select(ba => ba.DateOpened)
                .FirstOrDefaultAsync();
        }

        public object GetAccountAttribute(int userID, AccountAttribute attribute)
        {
            var userQuery = GetUserQuery(userID);

            if (attribute == AccountAttribute.Type)
            {
                return userQuery
                    .Select(u => u.UserType)
                    .FirstOrDefault();
            }
            else if (attribute == AccountAttribute.Alias)
            {
                return userQuery
                    .Select(u => u.Alias)
                    .FirstOrDefault();
            }
            else
            {
                throw new ArgumentException("Invalid format specified", nameof(attribute));
            }
        }

        public async Task<object> GetAccountAttributeAsync(int userID, AccountAttribute attribute)
        {
            var userAccountQuery = GetUserQuery(userID);

            if (attribute == AccountAttribute.Type)
            {
                return await userAccountQuery
                    .Select(u => u.UserType)
                    .FirstOrDefaultAsync();
            }
            else if (attribute == AccountAttribute.Alias)
            {
                return await userAccountQuery
                    .Select(u => u.Alias)
                    .FirstOrDefaultAsync();
            }
            else
            {
                throw new ArgumentException("Invalid format specified", nameof(attribute));
            }
        }

        public BankAccount GetBankAccountFromAlias(string alias)
        {
            return BankAccounts
                .Where(ba => ba.User.Alias == alias)
                .FirstOrDefault();
        }

        public async Task<BankAccount> GetBankAccountFromAliasAsync(string alias)
        {
            return await BankAccounts
                .Where(ba => ba.User.Alias == alias)
                .FirstOrDefaultAsync();
        }

        public Card GetCardFromPKey(int cardID)
        {
            return Cards
                .Where(c => c.CardID == cardID)
                .FirstOrDefault();
        }

        public async Task<Card> GetCardFromPKeyAsync(int cardID)
        {
            return await Cards
                .Where(c => c.CardID == cardID)
                .FirstOrDefaultAsync();
        }

        public Card GetCardFromCardNumber(string cardNumber)
        {
            return Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefault();
        }

        public async Task<Card> GetCardFromFromCardNumberAsync(string cardNumber)
        {
            return await Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefaultAsync();
        }

        public List<Card> GetCardsList(int userID)
        {
            return Cards
                .Where(c => c.AssociatedBankAccount.Holder == userID)
                .ToList();
        }

        public async Task<List<Card>> GetCardsListAsync(int userID)
        {
            return await Cards
                .Where(c => c.AssociatedBankAccount.Holder == userID)
                .ToListAsync();
        }

        public bool CheckExistingCard(string cardNumber)
        {
            Card card = Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefault();

            return (card != null) ? true : false;
        }

        public async Task<bool> CheckExistingCardAsync(string cardNumber)
        {
            Card card = await Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefaultAsync();

            return (card != null) ? true : false;
        }

        public DateTime GetLastCreatedCardTimestamp(int userID)
        {
            return Cards
                .Where(c => c.AssociatedBankAccount.Holder == userID)
                .OrderByDescending(c => c.IssueDate)
                .Select(c => c.IssueDate)
                .FirstOrDefault();
        }

        public async Task<DateTime> GetLastCreatedCardTimestampAsync(int userID)
        {
            return await Cards
                .Where(c => c.AssociatedBankAccount.Holder == userID)
                .OrderByDescending(c => c.IssueDate)
                .Select(c => c.IssueDate)
                .FirstOrDefaultAsync();
        }

        public List<TransactionRecord> GetTransactionRecordsList(int userID)
        {
            return TransactionRecords
                .Where(tr => tr.SenderAccount.Holder == userID || tr.RecipientAccount.Holder == userID)
                .ToList();
        }

        public Task<List<TransactionRecord>> GetTransactionRecordsListAsync(int userID)
        {
            return TransactionRecords
                .Where(tr => tr.SenderAccount.Holder == userID || tr.RecipientAccount.Holder == userID)
                .ToListAsync();
        }

        public TransactionRecord GetTransactionRecordFromCertificate(string certificate)
        {
            return TransactionRecords
                .Where(tr => tr.Certificate == certificate)
                .FirstOrDefault();
        }

        public async Task<TransactionRecord> GetTransactionRecordAsyncFromCertificate(string certificate)
        {
            return await TransactionRecords
                .Where(tr => tr.Certificate == certificate)
                .FirstOrDefaultAsync();
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Customize the table names by using EF's Fluent API. Other configurations can be done here too.
            // Overrides data annotations that are defined in the database-representative tables:
            modelBuilder.Entity<User>().Property(u => u.Id).HasColumnName("UserID");
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<CustomRole>().ToTable("Roles");
            modelBuilder.Entity<CustomUserRole>().ToTable("UserRoles");
            modelBuilder.Entity<CustomUserClaim>().ToTable("UserClaims");
            modelBuilder.Entity<CustomUserLogin>().ToTable("UserLogins");
            modelBuilder.Entity<Card>().HasIndex(c => c.CardNumber).IsUnique();

            // Due to the cyclical relationship between a "Sender" and "Recipient" both leading to a BankAccount entry, Cascade ON DELETE must be prevented.
            // Must implement a customized manual cleanup operation when a BankAccount is deleted that involves a TransactionRecord.
            modelBuilder.Entity<TransactionRecord>()
                .HasRequired(tr => tr.SenderAccount)
                .WithMany()
                .HasForeignKey(tr => tr.Sender)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<TransactionRecord>()
                .HasRequired(tr => tr.RecipientAccount)
                .WithMany()
                .HasForeignKey(tr => tr.Recipient)
            .WillCascadeOnDelete(false);

            modelBuilder.Entity<User>()
                .HasIndex(account => account.Email)
                .IsUnique();
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}