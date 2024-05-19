using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BankingApp.Models
{
    [Table("TransactionRecords")]
    public class TransactionRecord
    {
        [Key]
        public int TransactionID { get; set; }
        [Required]
        public int Sender { get; set; }
        [Required]
        public int Recipient { get; set; }
        [Required]
        public decimal Amount { get; set; }
        [Required]
        public DateTime TimeExecuted { get; set; }
        [Required]
        public bool Pending { get; set; }
        // Navigation fields for the foreign key "Sender" and "Recipient" relationships for BankAccount's AccountID key.
        [ForeignKey("Sender")]
        public virtual BankAccount SenderAccount { get; set; }
        [ForeignKey("Recipient")]
        public virtual BankAccount RecipientAccount { get; set; }
    }
}