using System;
using System.Data.Entity;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity.EntityFramework;

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
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}