using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BankingApp.Models
{
    [Table("BankAccounts")]
    public class BankAccount
    {
        [Key]
        public int AccountID { get; set; }
        [Required]
        public decimal Balance { get; set; }
        [Required]
        public int Holder { get; set; }
        public string PermissionKey { get; set; }
        [Required]
        public DateTime DateOpened { get; set; }
        [Required]
        public AccountType AccountType { get; set; }
        // Navigation field for the foreign key "Holder" relationship for the User's CustomerID key.
        [ForeignKey("Holder")]
        public virtual User User { get; set; }
    }

    public enum AccountType {
        Checking,
        Savings
    }
}