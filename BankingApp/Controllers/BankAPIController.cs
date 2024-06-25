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
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Description;
using System.Web.Routing;
using BankingApp.Models;
using BankingApp.Utilities;
using Microsoft.Ajax.Utilities;

namespace BankingApp.Controllers
{
    public class BankAPIController : ApiController
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private readonly Cryptography _cryptography;

        public BankAPIController()
        {
            _cryptography = new Cryptography();
        }


        [HttpGet]
        [Route("api/VerifyCard")]
       public IHttpActionResult VerifyCardStatus(string encryptedCardNumber, string encryptedKeyID)
        {
            Log.Info($"Verifying card status for this card...");
            
            if (string.IsNullOrWhiteSpace(encryptedCardNumber) || string.IsNullOrWhiteSpace(encryptedKeyID))
            {
                Log.Warn("Card number and key ID must be provided");
                return BadRequest("Card number and key ID must be provided.");
            }

            try
            {
                var decryptedID = _cryptography.DecryptID(encryptedKeyID);
                var decryptedCardNumber = _cryptography.DecryptID(encryptedCardNumber);
                var card = GetCard(decryptedCardNumber);
                
                if (card == null)
                {
                    Log.Warn($"Card with number {decryptedCardNumber} not found.");
                    return NotFound();
                }

                int decryptedIDNum = Int32.Parse(decryptedID);
                IHttpActionResult cardStatus = ValidateCardStatus(card, decryptedIDNum);
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

        [HttpGet]
        [Route("api/InitiateTransaction")]
        public IHttpActionResult InitiateTransaction(string encryptedCardNumber, string encryptedKeyID, string vendorAccountAlias, decimal paymentAmount)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encryptedCardNumber) || string.IsNullOrWhiteSpace(vendorAccountAlias))
                {
                    return BadRequest("Card number and vendor account name must be provided.");
                }

                string decryptedID = _cryptography.DecryptID(encryptedKeyID);
                string decryptedCardNumber = _cryptography.DecryptID(encryptedCardNumber);
                var card = GetCard(decryptedCardNumber);
                var customerBankAccount = card.AssociatedBankAccount;
                var vendorBankAccount = db.BankAccounts
                    .Where(ba => ba.User.Alias == vendorAccountAlias)
                    .FirstOrDefault();

                if (card == null)
                {
                    Log.Warn($"Card with number {decryptedCardNumber} not found.");
                    return NotFound();
                }

                if (vendorBankAccount == null || customerBankAccount == null)
                {
                    Log.Warn($"Could not find the vendor bank account or customer bank account.");
                    return NotFound();
                }

                Log.Info($"Initiating transaction with customer: {vendorBankAccount.User.Alias} and {vendorAccountAlias}");
                IHttpActionResult cardStatus = ValidateCardStatus(card, Int32.Parse(decryptedID));

                // Cannot continue with payment if the card isn't active or the keyIDs don't match.
                if (cardStatus == Unauthorized())
                {
                    Log.Warn($"Verify Card Status API returned: unauthorized.");
                    return cardStatus;
                }

                if (customerBankAccount.Balance < paymentAmount)
                {
                    Log.Warn("Not enough funds for this transaction of purchase");
                    return BadRequest("Not enough funds to complete the purchase.");
                }

                vendorBankAccount.Balance += paymentAmount;
                customerBankAccount.Balance -= paymentAmount;

                var certificate = _cryptography.GenerateRandomCertificate(12);

                TransactionRecord transactionRecord = new TransactionRecord()
                {
                    Sender = customerBankAccount.AccountID,
                    Recipient = vendorBankAccount.AccountID,
                    Amount = paymentAmount,
                    Description = $"Payment - Storefront, {vendorAccountAlias}",
                    Certificate = certificate,
                    TimeExecuted = DateTime.Now
                };

                try
                {
                    db.TransactionRecords.Add(transactionRecord);
                    db.SaveChanges();
                    Log.Info("Payment from the storefront is complete.");
                    return Ok($"Payment complete. Certificate: {certificate}");
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private IHttpActionResult ValidateCardStatus(Card card, int keyID)
        {
            if (!card.Active || card.KeyID != keyID)
            {
                Log.Warn("Card is inactive or the keyID didn't match.");
                return Unauthorized();
            }

            Log.Info("Validation success. Card is available for use.");
            return Ok("Verification complete. Card is available for use.");
        }

        private Card GetCard(string cardNumber)
        {
            return db.Cards
                .Where(c => c.CardNumber == cardNumber)
                .FirstOrDefault();
        }

        private BankAccount GetBankAccount(string userName)
        {
            return db.BankAccounts
                .Where(ba => ba.User.UserName == userName)
                .FirstOrDefault();
        }

        private bool CardExists(int id)
        {
            return db.Cards.Count(e => e.CardID == id) > 0;
        }
    }
}