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
       public IHttpActionResult VerifyCardStatus(string cardNumber, string encryptedKeyID)
        {
            Log.Info($"Verifying card status for card number: {cardNumber}");
            
            if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(encryptedKeyID))
            {
                Log.Warn("Card number and key ID must be provided");
                return BadRequest("Card number and key ID must be provided.");
            }

            var card = GetCard(cardNumber);
            if (card == null)
            {
                Log.Warn($"Card with number {cardNumber} not found.");
                return NotFound();
            }

            try
            {
                int decryptedID = _cryptography.DecryptID(encryptedKeyID);

                IHttpActionResult cardStatus = ValidateCardStatus(card, decryptedID);
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
        public IHttpActionResult InitiateTransaction(string cardNumber, string encryptedKeyID, string vendorAccountAlias, string itemType, int paymentAmount)
        {
            try
            {
                var card = GetCard(cardNumber);
                var customerBankAccount = card.AssociatedBankAccount;
                var vendorBankAccount = db.BankAccounts
                    .Where(ba => ba.User.Alias == vendorAccountAlias)
                    .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(vendorAccountAlias))
                {
                    return BadRequest("Card number and vendor account name must be provided.");
                }

                if (card == null || vendorBankAccount == null || customerBankAccount == null)
                {
                    return NotFound();
                }

                if (customerBankAccount.AccountType == AccountType.Savings)
                {
                    // Do things here to restrict it?
                }

                int decryptedID = _cryptography.DecryptID(encryptedKeyID);
                IHttpActionResult cardStatus = ValidateCardStatus(card, decryptedID);

                // Cannot continue with payment if the card isn't active or the keyIDs don't match.
                if (cardStatus == BadRequest("Card is not active.") || cardStatus == Unauthorized())
                {
                    return cardStatus;
                }

                if (customerBankAccount.Balance < paymentAmount)
                {
                    return BadRequest("Not enough funds to do the purchase.");
                }

                vendorBankAccount.Balance += paymentAmount;
                customerBankAccount.Balance -= paymentAmount;

                TransactionRecord transactionRecord = new TransactionRecord()
                {
                    Sender = customerBankAccount.AccountID,
                    Recipient = vendorBankAccount.AccountID,
                    Amount = paymentAmount,
                    Description = $"Payment - {itemType}",
                    TimeExecuted = DateTime.Now
                };

                db.TransactionRecords.Add(transactionRecord);
                int entityChangesCount = db.SaveChanges();

                if (entityChangesCount > 0)
                {
                    return Ok("Payment complete.");
                }
                else
                {
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