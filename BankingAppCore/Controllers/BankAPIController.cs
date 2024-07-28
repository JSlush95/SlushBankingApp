using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BankingAppCore.Models;
using BankingAppCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BankingAppCore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BankAPIController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Cryptography _cryptography;
        private readonly ILogger<BankAPIController> _logger;

        public BankAPIController(ApplicationDbContext dbContext, Cryptography cryptography, ILogger<BankAPIController> logger)
        {
            _cryptography = cryptography;
            _dbContext = dbContext;
            _logger = logger;
        }

        // Deserialized transaction request from the storefront
        public class TransactionRequest
        {
            public string EncryptedCardNumber { get; set; }
            public string EncryptedKeyPIN { get; set; }
            public List<VendorTransaction> VendorTransactions { get; set; }
        }

        // Deserialized vendor information from the storefront
        public class VendorTransaction
        {
            public string VendorAlias { get; set; }
            public decimal TotalAmount { get; set; }
        }

        // Deserialized refund request from the storefront
        public class RefundRequest
        {
            public List<string> Certificates { get; set; }
            public List<decimal> Amounts { get; set; }
        }

        [HttpGet("VerifyCard")]
        [Authorize(Policy ="AuthorizeAliasPolicy")]
        public IActionResult VerifyCardStatus(string encryptedCardNumber, string encryptedKeyPIN)
        {
            _logger.LogInformation("Verifying card status for this card...");

            if (string.IsNullOrWhiteSpace(encryptedCardNumber) || string.IsNullOrWhiteSpace(encryptedKeyPIN))
            {
                _logger.LogWarning("Card number and key ID must be provided");
                return BadRequest("Card number and key ID must be provided.");
            }

            try
            {
                string decryptedKeyPIN = _cryptography.DecryptItem(encryptedKeyPIN);
                string decryptedCardNumber = _cryptography.DecryptItem(encryptedCardNumber);
                Card card = _dbContext.GetCardFromCardNumber(decryptedCardNumber);

                if (card == null)
                {
                    _logger.LogWarning("Card not found.");
                    return NotFound();
                }

                IActionResult cardStatus = ValidateCardStatus(card, decryptedKeyPIN);
                _logger.LogInformation($"Verify Card Status API returned: {cardStatus}.");

                return cardStatus;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError($"An error occurred during decryption.\n{ex}");
                return BadRequest("An error occurred during decryption.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while verifying card status.\n{ex}");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost("InitiateTransaction")]
        [Authorize(Policy = "AuthorizeAliasPolicy")]
        public IActionResult InitiateTransaction(TransactionRequest request)
        {
            try
            {
                _logger.LogInformation("Initiating transaction...");

                if (string.IsNullOrWhiteSpace(request.EncryptedCardNumber) || string.IsNullOrWhiteSpace(request.EncryptedKeyPIN))
                {
                    _logger.LogWarning("Card number and KeyPIN must be provided.");
                    return BadRequest("Card number and KeyPIN must be provided.");
                }

                string decryptedKeyPIN = _cryptography.DecryptItem(request.EncryptedKeyPIN);
                string decryptedCardNumber = _cryptography.DecryptItem(request.EncryptedCardNumber);
                Card card = _dbContext.GetCardFromCardNumber(decryptedCardNumber);
                BankAccount customerBankAccount = card.AssociatedBankAccount;
                _logger.LogWarning($"Decrypted KeyPIN: {decryptedKeyPIN} and Number: {decryptedCardNumber}. Customer Bank Account exists? {customerBankAccount != null}.");

                if (card == null || customerBankAccount == null)
                {
                    _logger.LogWarning($"Card or customer bank account not found. Card: {card.CardNumber} and {card.AssociatedBankAccount}");
                    return NotFound();
                }

                IActionResult cardStatus = ValidateCardStatus(card, decryptedKeyPIN);

                if (cardStatus is UnauthorizedResult)
                {
                    _logger.LogWarning("Verify Card Status API returned: unauthorized.");
                    return cardStatus;
                }

                decimal totalAmount = request.VendorTransactions.Sum(vt => vt.TotalAmount);

                if (customerBankAccount.Balance < totalAmount)
                {
                    _logger.LogWarning("Not enough funds for this transaction.");
                    return BadRequest("Not enough funds to complete the purchase.");
                }

                List<string> certificates = new List<string>();

                foreach (var transaction in request.VendorTransactions)
                {
                    string decryptedVendorAccountAlias = _cryptography.DecryptItem(transaction.VendorAlias);
                    BankAccount vendorBankAccount = _dbContext.GetBankAccountFromAlias(decryptedVendorAccountAlias);

                    if (vendorBankAccount == null)
                    {
                        _logger.LogWarning($"Vendor bank account not found for alias: {vendorBankAccount.User.Alias} and {decryptedVendorAccountAlias}");
                        return NotFound();
                    }

                    vendorBankAccount.Balance += transaction.TotalAmount;
                    customerBankAccount.Balance -= transaction.TotalAmount;

                    string certificate = _cryptography.GenerateRandomCertificate(12);
                    certificates.Add(certificate);

                    TransactionRecord transactionRecord = new TransactionRecord()
                    {
                        Sender = customerBankAccount.AccountID,
                        Recipient = vendorBankAccount.AccountID,
                        Amount = transaction.TotalAmount,
                        Status = TransactionStatus.Approved,
                        TransactionType = TransactionType.Purchase,
                        Description = "Purchase from the companion storefront",
                        Certificate = certificate,
                        TimeExecuted = DateTime.Now
                    };

                    _dbContext.TransactionRecords.Add(transactionRecord);
                }

                try
                {
                    _dbContext.SaveChanges();
                    _logger.LogInformation("Payment from the storefront is complete.");
                    return Ok(new { Certificates = certificates });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during DB transaction.");
                    return StatusCode(500, "Internal server error.");
                }
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Invalid format for the provided parameters.");
                return BadRequest("Invalid format for the provided parameters.");
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "An error occurred during decryption.");
                return BadRequest("An error occurred during decryption.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Internal server error.");
                return StatusCode(500, "Internal server error.");
            }
        }

        [HttpPost]
        [Route("InitiateRefund")]
        [Authorize(Policy = "AuthorizeAliasPolicy")]
        public IActionResult InitiateRefund(RefundRequest request)
        {
            _logger.LogInformation("Initiating refund...");

            if (request.Certificates == null || request.Amounts == null || request.Certificates.Count != request.Amounts.Count)
            {
                return BadRequest("Invalid refund request.");
            }

            // Getting the current user's alias from the request context.
            string authHeader = Request.Headers["Authorization"].ToString();
            string requestAlias = authHeader["Alias".Length..].Trim();
            string decryptedAlias = _cryptography.DecryptItem(requestAlias);

            try
            {
                // Using the de-serialized Request object from the API payload to handle the refund transaction.
                for (int i = 0; i < request.Certificates.Count; i++)
                {
                    string certificate = request.Certificates[i];
                    TransactionRecord transactionRecord = _dbContext.GetTransactionRecordFromCertificate(certificate);

                    if (transactionRecord == null)
                    {
                        _logger.LogWarning("Transaction record not found for certificate: {Certificate}", request.Certificates[i]);
                        return NotFound();
                    }

                    // Additional security protocol, the alias of the related transaction record must have the same alias as the request.
                    if (transactionRecord.SenderAccount.User.Alias != decryptedAlias)
                    {
                        _logger.LogWarning($"Unauthorized attempt to refund certificate, alias mismatch for certificate: {request.Certificates[i]}");
                        return Unauthorized();
                    }

                    BankAccount customerAccount = transactionRecord.SenderAccount;
                    BankAccount vendorAccount = transactionRecord.RecipientAccount;
                    _logger.LogInformation($"Original balances: Customer ({customerAccount.Balance}), Vendor ({vendorAccount.Balance})");

                    customerAccount.Balance += request.Amounts[i];
                    vendorAccount.Balance -= request.Amounts[i];
                    _logger.LogInformation($"Updated balances after refund: Customer ({customerAccount.Balance}), Vendor ({vendorAccount.Balance})");

                    TransactionRecord refundRecord = new TransactionRecord()
                    {
                        Sender = vendorAccount.AccountID,
                        Recipient = customerAccount.AccountID,
                        Description = "Refund from the companion storefront",
                        Amount = request.Amounts[i],
                        TransactionType = TransactionType.Refund,
                        Certificate = _cryptography.GenerateRandomCertificate(12),
                        TimeExecuted = DateTime.Now,
                        Status = TransactionStatus.Approved
                    };

                    transactionRecord.Status = TransactionStatus.Refunded;

                    _dbContext.TransactionRecords.Add(refundRecord);
                }

                try
                {
                    _dbContext.SaveChanges();
                    _logger.LogInformation("Refund from the storefront is complete.");
                    return Ok("Refund successful");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during DB transaction, under an initiated refund.");
                    return StatusCode(500, "Internal server error.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during refund processing.");
                return StatusCode(500, "Internal server error.");
            }
        }

        private IActionResult ValidateCardStatus(Card card, string keyPIN)
        {
            _logger.LogInformation("Validating Card Status");
            bool valid = card.ValidatePINWithWebAPICall(keyPIN);

            if (!valid)
            {
                _logger.LogWarning("Card is inactive or the KeyPIN didn't match.");
                return Unauthorized();
            }
            else
            {
                _logger.LogInformation("Verification success. Card is available for use.");
                return Ok("Verification complete. Card is available for use.");
            }
        }
    }
}
