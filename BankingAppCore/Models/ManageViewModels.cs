﻿using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CompareAttribute = System.ComponentModel.DataAnnotations.CompareAttribute;

namespace BankingAppCore.Models
{
    public class IndexViewModel
    {
        public bool HasPassword { get; set; }
        public IList<UserLoginInfo> Logins { get; set; }
        public bool TwoFactor { get; set; }
        public bool EmailConfirmed { get; set; }
        public UserType AccountType {  get; set; }
        public string AccountAlias { get; set; }
        [Display(Name = "Alias Name")]
        public string AliasFormInput { get; set; }
        public bool AccountAliasSet { get; set; }
        public CreateAccountViewModel CreateAccountViewModel { get; set; }
        public CreateCardViewModel CreateCardViewModel { get; set; }
        public TransferFundsViewModel TransferFundsViewModel { get; set; }
        public DisplayAccountInfoViewModel DisplayAccountInfoViewModel { get; set; }
        public DisplayTransferInfoViewModel DisplayTransferInfoViewModel { get; set; }
    }

    public class FactorViewModel
    {
        public string Purpose { get; set; }
    }

    public class ChangeEmailViewModel
    {
        [Required]
        [Display(Name = "Current email")]
        public string OldEmail { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
        [Display(Name = "New email")]
        public string NewEmail { get; set; }

        [Display(Name = "Confirm new email")]
        [Compare("NewEmail", ErrorMessage = "The new email and confirmation email do not match.")]
        public string ConfirmEmail { get; set; }
    }

    public class ChangeUsernameViewModel
    {
        [Required]
        [Display(Name = "Current username")]
        public string OldUsername { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [Display(Name = "New username")]
        public string NewUsername { get; set; }

        [Display(Name = "Confirm new username")]
        [Compare("NewUsername", ErrorMessage = "The new username and confirmation username do not match.")]
        public string ConfirmUsername { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string OldPassword { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class ConfigureTwoFactorViewModel
    {
        public string SelectedProvider { get; set; }
        public ICollection<SelectListItem> Providers { get; set; }
    }

    public class DisplayAccountInfoViewModel
    {
        public List<BankAccount> Accounts { get; set; }
        public List<Card> Cards { get; set; }

        [Display(Name = "Amount")]
        public decimal Amount { get; set; }
    }

    public class DisplayTransferInfoViewModel
    {
        public List<TransactionRecord> Transactions { get; set; }
    }

    public class CreateAccountViewModel
    {
        [Required(ErrorMessage = "Please select a valid account type")]
        [Display(Name = "Account Type")]
        public AccountType AccountType { get; set; }

        [Required(ErrorMessage = "Please enter the amount to transfer")]
        [Display(Name = "Balance")]
        public decimal Balance { get; set; }
    }

    public class CreateCardViewModel
    {
        [Required(ErrorMessage = "Please select an account")]
        [Display(Name = "Bank Account ID")]
        public int SelectedAccountID { get; set; }

        [Required(ErrorMessage = "Please select a valid non-decimal, non-negative number with 5 digits.")]
        [Display(Name = "Key PIN")]
        [StringLength(5)]
        public string KeyPIN { get; set; }

        [Required(ErrorMessage = "Please select a card type")]
        [Display(Name = "Card Type")]
        public CardType CardType { get; set; }

        public IEnumerable<SelectListItem> BankAccounts { get; set; }
    }

    public class TransferFundsViewModel
    {
        [Required(ErrorMessage = "Please select the amount to send")]
        [Display(Name = "Source Account")]
        public int SourceAccountId { get; set; }

        [Required(ErrorMessage = "Please select the destination account")]
        [Display(Name = "Destination Account")]
        public int DestinationAccountId { get; set; }

        [Required(ErrorMessage = "Please enter the amount to transfer")]
        [Range(0.01, Double.MaxValue, ErrorMessage = "Please enter a valid amount")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        public IEnumerable<SelectListItem> SourceAccounts { get; set; }
        public IEnumerable<SelectListItem> DestinationAccounts { get; set; }
    }
}