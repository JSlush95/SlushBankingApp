using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using System.Web.Routing;
using BankingApp.Models;
using BankingApp.Utilities;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;

namespace BankingApp.Controllers
{
    public class BankAPIController : ApiController
    {
        private ApplicationDbContext _dbContext;
        private readonly Cryptography _cryptography;

        public BankAPIController()
        {
            _cryptography = new Cryptography();
            _dbContext = new ApplicationDbContext();
        }

        // Deserialized transaction request from the storefront.
        public class TransactionRequest
        {
            public string EncryptedCardNumber { get; set; }
            public string EncryptedKeyPIN { get; set; }
            public List<VendorTransaction> VendorTransactions { get; set; }
        }

        // Deserialized vendor information from the storefront.
        public class VendorTransaction
        {
            public string VendorAlias { get; set; }
            public decimal TotalAmount { get; set; }
        }

        // Deserialized refund request from the storefront.
        public class RefundRequest
        {
            public List<string> Certificates { get; set; }
            public List<decimal> Amounts { get; set; }
        }

        [HttpGet]
        [Route("api/VerifyCard")]
        [EnableCors(origins: "https://localhost:44396/", headers: "*", methods: "*")]
        [AuthorizeAlias]
        public IHttpActionResult VerifyCardStatus(string encryptedCardNumber, string encryptedKeyPIN)
        {
            Log.Info($"Verifying card status for this card...");

           if (string.IsNullOrWhiteSpace(encryptedCardNumber) || string.IsNullOrWhiteSpace(encryptedKeyPIN))
            {
                Log.Warn("Card number and key ID must be provided");
                return BadRequest("Card number and key ID must be provided.");
            }

            try
            {
                var decryptedID = _cryptography.DecryptItem(encryptedKeyPIN);
                var decryptedCardNumber = _cryptography.DecryptItem(encryptedCardNumber);
                var card = GetCard(decryptedCardNumber);

                if (card == null)
                {
                    Log.Warn($"Card not found.");
                    return NotFound();
                }

                IHttpActionResult cardStatus = ValidateCardStatus(card, decryptedID);
                Log.Info($"Verify Card Status API returned: {cardStatus}.");

                return cardStatus;
            }
            catch (CryptographicException ex)
            {
                Log.Error("An error occurred during decryption.", ex);
                return BadRequest("An error occurred during decryption.");
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred while verifying card status.", ex);
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/InitiateTransaction")]
        [EnableCors(origins: "https://localhost:44396/", headers: "*", methods: "*")]
        [AuthorizeAlias]
        public IHttpActionResult InitiateTransaction(TransactionRequest request)
        {
            try
            {
                Log.Info("Initiating transaction...");

                if (string.IsNullOrWhiteSpace(request.EncryptedCardNumber) || string.IsNullOrWhiteSpace(request.EncryptedKeyPIN))
                {
                    return BadRequest("Card number and KeyPIN must be provided.");
                }

                string decryptedKeyPIN = _cryptography.DecryptItem(request.EncryptedKeyPIN);
                string decryptedCardNumber = _cryptography.DecryptItem(request.EncryptedCardNumber);
                var card = GetCard(decryptedCardNumber);
                var customerBankAccount = card.AssociatedBankAccount;

                if (card == null || customerBankAccount == null)
                {
                    Log.Warn($"Card or customer bank account not found.");
                    return NotFound();
                }

                IHttpActionResult cardStatus = ValidateCardStatus(card, decryptedKeyPIN);

                if (cardStatus == Unauthorized())
                {
                    Log.Warn($"Verify Card Status API returned: unauthorized.");
                    return cardStatus;
                }

                decimal totalAmount = request.VendorTransactions.Sum(vt => vt.TotalAmount);

                if (customerBankAccount.Balance < totalAmount)
                {
                    Log.Warn("Not enough funds for this transaction.");
                    return BadRequest("Not enough funds to complete the purchase.");
                }

                List<string> certificates = new List<string>();

                foreach (var transaction in request.VendorTransactions)
                {
                    string decryptedVendorAccountAlias = _cryptography.DecryptItem(transaction.VendorAlias);
                    var vendorBankAccount = _dbContext.BankAccounts
                        .Where(ba => ba.User.Alias == decryptedVendorAccountAlias)
                        .FirstOrDefault();

                    if (vendorBankAccount == null)
                    {
                        Log.Warn($"Vendor bank account not found for alias: {decryptedVendorAccountAlias}");
                        return NotFound();
                    }

                    vendorBankAccount.Balance += transaction.TotalAmount;
                    customerBankAccount.Balance -= transaction.TotalAmount;

                    var certificate = _cryptography.GenerateRandomCertificate(12);
                    certificates.Add(certificate);

                    TransactionRecord transactionRecord = new TransactionRecord()
                    {
                        Sender = customerBankAccount.AccountID,
                        Recipient = vendorBankAccount.AccountID,
                        Amount = transaction.TotalAmount,
                        Status = TransactionStatus.Approved,
                        Description = $"Purchase - {vendorBankAccount.User.UserName}",
                        Certificate = certificate,
                        TimeExecuted = DateTime.Now
                    };

                    _dbContext.TransactionRecords.Add(transactionRecord);
                }

                try
                {
                    _dbContext.SaveChanges();
                    Log.Info("Payment from the storefront is complete.");
                    return Ok(new { Certificates = certificates });
                }
                catch (Exception ex)
                {
                    Log.Warn($"Error occurred during DB transaction. {ex}");
                    return InternalServerError();
                }
            }
            catch (FormatException ex)
            {
                return BadRequest("Invalid format for the provided parameters.");
            }
            catch (CryptographicException ex)
            {
                return BadRequest("An error occurred during decryption.");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/InitiateRefund")]
        [EnableCors(origins: "https://localhost:44396/", headers: "*", methods: "*")]
        [AuthorizeAlias]
        public IHttpActionResult InitiateRefund(RefundRequest request)
        {
            Log.Info("Initiating refund...");

            if (request.Certificates == null || request.Amounts == null || request.Certificates.Count != request.Amounts.Count)
            {
                return BadRequest("Invalid refund request.");
            }

            try
            {
                // Using the de-serialized Request object from the API payload to handle the refund transaction.
                for (int i = 0; i < request.Certificates.Count; i++)
                {
                    var certificate = request.Certificates[i];
                    // Cannot use 'tr.Certificate == reqest.Certificates[i]' statement because it needs to be a direct SQL-compatible statement instead. Hence the above.
                    var transactionRecord = _dbContext.TransactionRecords
                        .Where(tr => tr.Certificate == certificate)
                        .FirstOrDefault();

                    if (transactionRecord == null)
                    {
                        Log.Warn($"Transaction record not found for certificate: {request.Certificates[i]}");
                        return NotFound();
                    }

                    var customerAccount = transactionRecord.SenderAccount;
                    var vendorAccount = transactionRecord.RecipientAccount;
                    Log.Info($"Original balances: Customer {customerAccount.Balance}, Vendor {vendorAccount.Balance}");

                    customerAccount.Balance += request.Amounts[i];
                    vendorAccount.Balance -= request.Amounts[i];
                    Log.Info($"Updated balances after refund: Customer {customerAccount.Balance}, Vendor {vendorAccount.Balance}");

                    TransactionRecord refundRecord = new TransactionRecord()
                    {
                        Sender = vendorAccount.AccountID,
                        Recipient = customerAccount.AccountID,
                        Description = $"Refund - {vendorAccount.User.UserName}",
                        Amount = request.Amounts[i],
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
                    Log.Info("Refund from the storefront is complete.");
                    return Ok("Refund successful");
                }
                catch (Exception ex)
                {
                    Log.Warn($"Error occurred during DB transaction, under an initiated refund. {ex}");
                    return InternalServerError();
                }
            }
            catch (Exception ex)
            {
                Log.Error("An error occurred during refund processing.", ex);
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }
            base.Dispose(disposing);
        }

        private IHttpActionResult ValidateCardStatus(Card card, string KeyPIN)
        {
            Log.Info($"Validating Card Status: {card.KeyPIN} and {KeyPIN}");
            if (!card.Active || card.KeyPIN != KeyPIN)
            {
                Log.Warn("Card is inactive or the KeyPIN didn't match.");
                return Unauthorized();
            }

            Log.Info("Validation success. Card is available for use.");
            return Ok("Verification complete. Card is available for use.");
        }

        private Card GetCard(string cardNumber)
        {
            Log.Info($"Getting card from {cardNumber}.");
            return _dbContext.Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefault();
        }
    }
}