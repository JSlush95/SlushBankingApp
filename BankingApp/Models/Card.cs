using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BankingApp.Models
{
    [Table("Cards")]
    public class Card
    {
        [Key]
        public int CardID { get; set; }
        [Required]
        public CardType CardType { get; set; }
        [Required]
        [StringLength(11)]
        public string CardNumber { get; set; }
        [Required]
        [Range(10000, 99999)]
        public int KeyID { get; set; }
        [Required]
        public int AssociatedAccount { get; set; }
        [Required]
        public DateTime IssueDate { get; set; }
        [Required]
        public bool Active { get; set; }
        // Navigation field for the foreign key "AssociatedAccount" relationship for the BankAccount's AccountID key.
        [ForeignKey("AssociatedAccount")]
        public virtual BankAccount AssociatedBankAccount { get; set; }
    }

    public enum CardType
    {
        Credit,
        Debit
    }
}