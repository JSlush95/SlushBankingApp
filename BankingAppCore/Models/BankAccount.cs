using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BankingAppCore.Models
{
    [Table("BankAccounts")]
    public class BankAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountID { get; set; }
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Balance { get; set; }
        [Required]
        public int? Holder { get; set; }
        [Required]
        public bool Active { get; set; } = true;
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