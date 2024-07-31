using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BankingAppCore.Models
{
    [Table("TransactionRecords")]
    public class TransactionRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TransactionID { get; set; }
        [Required]
        public int Sender { get; set; }
        [Required]
        public int Recipient { get; set; }
        public string Certificate { get; set; }
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Amount { get; set; }
        [Required]
        public TransactionType TransactionType { get; set; }
        public string Description { get; set; }
        [Required]
        public DateTime TimeExecuted { get; set; }
        [Required]
        public TransactionStatus Status { get; set; }

        // Navigation fields for the foreign key "Sender" and "Recipient" relationships for BankAccount's AccountID key.
        [ForeignKey("Sender")]
        public virtual BankAccount SenderAccount { get; set; }
        [ForeignKey("Recipient")]
        public virtual BankAccount RecipientAccount { get; set; }
    }

    public enum TransactionStatus
    {
        Pending,
        Approved,
        Refunded
    }

    public enum TransactionType
    {
        Transfer,
        Deposit,
        Purchase,
        Refund,
        Interest
    }
}