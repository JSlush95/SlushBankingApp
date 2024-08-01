using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using BankingAppCore.Models;
using BankingAppCore.Data;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace BankingAppCore.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ManageController> _logger;
        private readonly IEmailSender _emailService;

        public ManageController(ApplicationDbContext context, UserManager<User> userManager, SignInManager<User> signInManager, ILogger<ManageController> logger, IEmailSender emailService)
        {
            _dbContext = context;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailService = emailService;
        }

        private int? GetCurrentUserId()
        {
            string userId = User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(userId, out int retVal))
            {
                return retVal;
            }

            return null;
        }

        private string? GetCurrentUsername()
        {
            return User?.FindFirstValue(ClaimTypes.Name);
        }

        private async Task<User> GetCurrentUserAsync(int? id)
        {
            return await _userManager.FindByIdAsync(id.ToString());
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
        }

        private async Task<bool> HasUsername()
        {
            var user = await _userManager.FindByNameAsync(GetCurrentUsername());

            if (user != null)
            {
                return user != null;
            }
            return false;
        }

        private async Task<bool> HasPassword()
        {
            var user = await _userManager.FindByIdAsync(GetCurrentUserId().ToString());

            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            ChangeUsernameSuccess,
            ChangeEmailSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private string GenerateRandomCardNumber(int stringLength)
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                // Calculate the amount of bytes
                var bit_count = (stringLength * 6);
                var byte_count = ((bit_count + 7) / 8); // Round up (nearest byte)

                var bytes = new byte[byte_count];
                rng.GetBytes(bytes);

                // Convert the byte array to a string
                var base64String = Convert.ToBase64String(bytes);

                // Return the required length of the string. Validate the length of the string
                return base64String.Length >= stringLength
                    ? base64String[..stringLength]
                    : base64String;
            }
        }

        private async Task<IndexViewModel> CreateIndexViewModel()
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            IndexViewModel indexModel = new IndexViewModel
            {
                HasPassword = await HasPassword(),
                TwoFactor = await _userManager.GetTwoFactorEnabledAsync(user),
                Logins = await _userManager.GetLoginsAsync(user),
                EmailConfirmed = user.EmailConfirmed
            };

            return indexModel;
        }

        // GET: /Manage/Index
        public async Task<IActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.ChangeUsernameSuccess ? "Your username has been changed."
                : message == ManageMessageId.ChangeEmailSuccess ? "Your email has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.SetTwoFactorSuccess ? "Your two-factor authentication provider has been set."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";

            int? userId = GetCurrentUserId();    // Must get the user's ID field for the LINQ functionality, as it cannot evaluate the function call directly
            
            List<BankAccount> activeBankAccounts = await _dbContext.GetActiveBankAccountsListAsync(userId);
            List<Card> cards = await _dbContext.GetActiveCardsListAsync(userId);
            List<TransactionRecord> transactions = await _dbContext.GetTransactionRecordsListAsync(userId);
            
            UserType accountType = (UserType)await _dbContext.GetAccountAttributeAsync(userId, ApplicationDbContext.AccountAttribute.Type);
            string accountAlias = (string)await _dbContext.GetAccountAttributeAsync(userId, ApplicationDbContext.AccountAttribute.Alias);

            if (TempData.ContainsKey("Message"))
            {
                ViewBag.Message = TempData["Message"];
            }

            if (TempData.ContainsKey("2FAMessage"))
            {
                ViewBag.Message2FA = TempData["2FAMessage"];
            }

            IndexViewModel model = await CreateIndexViewModel();
            model.CreateAccountViewModel = new CreateAccountViewModel();
            model.CreateCardViewModel = new CreateCardViewModel
            {
                BankAccounts = activeBankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType}"
                }).ToList()
            };
            model.TransferFundsViewModel = new TransferFundsViewModel
            {
                SourceAccounts = activeBankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType} - Balance: {ba.Balance}"
                }),
                DestinationAccounts = activeBankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType} - Balance: {ba.Balance}"
                })
            };
            model.DisplayAccountInfoViewModel = new DisplayAccountInfoViewModel
            {
                Accounts = activeBankAccounts,
                Cards = cards
            };
            model.DisplayTransferInfoViewModel = new DisplayTransferInfoViewModel
            {
                Transactions = transactions
            };
            model.AccountType = accountType;
            model.AccountAliasSet = (string.IsNullOrWhiteSpace(accountAlias))? false: true;
            model.AccountAlias = accountAlias;

            return View(model);
        }

        // POST: /Manage/SetAccountAlias
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetAccountAlias(string aliasFormInput)
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            bool existingUser = await _dbContext.CheckExistingUserAsync(aliasFormInput);

            if (existingUser)
            {
                TempData["Message"] = "Alias already exists, choose another.";
                return RedirectToAction(nameof(Index));
            }

            if (user != null)
            {
                user.Alias = aliasFormInput;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    TempData["Message"] = "Successfully set alias.";
                }
                else
                {
                    TempData["Message"] = "Failed to set alias.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Manage/CreateAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(CreateAccountViewModel model)
        {
            int? userId = GetCurrentUserId();
            User user = await GetCurrentUserAsync(userId);

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used when creating a new bank account, please follow the rules and try again.";
                return RedirectToAction(nameof(Index));
            }

            if (user.EmailConfirmed)
            {
                DateTime lastAccountCreationTimestamp = await _dbContext.GetLastCreatedBankAccountTimestampAsync(userId);
                BankAccount newAccount = new BankAccount
                {
                    Balance = model.Balance,
                    Holder = userId,
                    DateOpened = DateTime.Now,
                    AccountType = model.AccountType
                };

                TimeSpan cooldownPeriod = TimeSpan.FromMinutes(1);

                if (lastAccountCreationTimestamp != default && DateTime.Now - lastAccountCreationTimestamp < cooldownPeriod)
                {
                    TimeSpan timeRemaining = cooldownPeriod - (DateTime.Now - lastAccountCreationTimestamp);

                    if (timeRemaining > TimeSpan.Zero)
                    {
                        TempData["Message"] = $"Please wait {timeRemaining.TotalSeconds} before creating a new account.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                _dbContext.BankAccounts.Add(newAccount);
                await _dbContext.SaveChangesAsync();

                TempData["Message"] = "Success with creating a bank account.";
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Manage/AddFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFunds(int accountID, decimal? amount)
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            BankAccount account = await _dbContext.GetBankAccountFromPKeyAsync(accountID);

            if (user.EmailConfirmed)
            {
                if (!amount.HasValue || amount <= 0)
                {
                    TempData["Message"] = "Please enter a non-zero number amount.";
                }
                else if (account != null)
                {
                    TransactionRecord transaction = new TransactionRecord()
                    {
                        Sender = accountID,
                        Recipient = accountID,
                        Amount = (decimal)amount,
                        TransactionType = TransactionType.Deposit,
                        Description = "Added funds to the bank account",
                        TimeExecuted = DateTime.Now,
                    };

                    using (var transactionContext = await _dbContext.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            account.Balance += (decimal)amount;
                            _dbContext.TransactionRecords.Add(transaction);

                            await _dbContext.SaveChangesAsync();
                            await transactionContext.CommitAsync();

                            TempData["Message"] = $"Success with adding {amount} to AccountID: {accountID}";
                        }
                        catch
                        {
                            await transactionContext.RollbackAsync();
                            TempData["Message"] = "Failed to add funds.";
                        }
                    }
                }
                else
                {
                    TempData["Message"] = "Failed to retrieve the bank account.";
                }
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Manage/RemoveAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveAccount(int accountID)
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            BankAccount account = await _dbContext.GetBankAccountFromPKeyAsync(accountID);

            if (user.EmailConfirmed)
            {
                if(account != null)
                {
                    var relatedCards = await _dbContext.GetRelatedCardsFromBankAccountAsync(accountID);

                    foreach(var card in relatedCards)
                    {
                        card.Active = false;
                    }

                    account.Active = false;

                    //_dbContext.BankAccounts.Update(account);
                    await _dbContext.SaveChangesAsync();
                    TempData["Message"] = "Success with deleting the bank account.";
                }
                else
                {
                    TempData["Message"] = "Failed to delete the bank account.";
                }
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }
            
            return RedirectToAction("index");
        }

        // POST: /Manage/CreateCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCard(CreateCardViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used when creating a new card, please follow the rules and try again.";
                return RedirectToAction(nameof(Index));
            }

            User user = await GetCurrentUserAsync(GetCurrentUserId());
            if (user.EmailConfirmed)
            {
                DateTime lastCardCreationTimestamp = await _dbContext.GetLastCreatedCardTimestampAsync(user.Id);

                if (lastCardCreationTimestamp != default && DateTime.Now - lastCardCreationTimestamp < TimeSpan.FromMinutes(1))
                {
                    var timeRemaining = TimeSpan.FromMinutes(1) - (DateTime.Now - lastCardCreationTimestamp);

                    if (timeRemaining > TimeSpan.Zero)
                    {
                        TempData["Message"] = $"Please wait {timeRemaining.TotalSeconds} before creating a new card.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                string cardNumber;
                bool cardExists;

                do
                {
                    cardNumber = GenerateRandomCardNumber(11);
                    cardExists = await _dbContext.CheckExistingCardAsync(cardNumber);
                } while (!cardExists && cardNumber.Length != 11);

                var newCard = new Card()
                {
                    CardNumber = cardNumber,
                    CardType = model.CardType,
                    KeyPIN = model.KeyPIN,
                    AssociatedAccount = model.SelectedAccountID,
                    IssueDate = DateTime.Now,
                    ExpireDate = DateTime.Now.AddMonths(6),
                    Active = true
                };

                _dbContext.Cards.Add(newCard);
                await _dbContext.SaveChangesAsync();

                TempData["Message"] = "Success with creating a card.";
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Manage/RemoveCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCard(int cardID)
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            Card card = await _dbContext.GetCardFromPKeyAsync(cardID);

            if (user.EmailConfirmed)
            {
                if (card != null)
                {
                    _dbContext.Cards.Remove(card);
                    await _dbContext.SaveChangesAsync();
                    TempData["Message"] = "Success with deleting the card.";
                }
                else
                {
                    TempData["Message"] = "Failed to delete the card.";
                }
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }
            
            return RedirectToAction("Index");
        }

        // POST: /Manage/TransferFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferFunds(TransferFundsViewModel model)
        {
            User user = await GetCurrentUserAsync(GetCurrentUserId());
            BankAccount sourceAccount = await _dbContext.GetBankAccountFromPKeyAsync(model.SourceAccountId);
            BankAccount destinationAccount = await _dbContext.GetBankAccountFromPKeyAsync(model.DestinationAccountId);

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used for transferring, please follow the rules and try again.";
                return RedirectToAction("Index");
            }

            if (user.EmailConfirmed)
            {
                // Checking if both accounts exist and the source account has sufficient funds
                if (sourceAccount == null || destinationAccount == null)
                {
                    TempData["Message"] = "One or both of the bank accounts do not exist.";
                    
                    return RedirectToAction("Index");
                }

                if (sourceAccount.AccountID == destinationAccount.AccountID)
                {
                    TempData["Message"] = "Please choose different accounts to transfer funds.";
                    return RedirectToAction("Index");
                }

                if (sourceAccount.Balance < model.Amount)
                {
                    // Handle the case where the source account does not have sufficient funds
                    TempData["Message"] = "Insufficient funds in the source account.";

                    return RedirectToAction("Index");
                }

                destinationAccount.Balance += model.Amount;
                sourceAccount.Balance -= model.Amount;

                TransactionRecord transactionRecord = new TransactionRecord()
                {
                    Sender = sourceAccount.AccountID,
                    Recipient = destinationAccount.AccountID,
                    Amount = model.Amount,
                    TransactionType = TransactionType.Transfer,
                    Status = TransactionStatus.Approved,
                    Description = "Transfer from another bank account",
                    TimeExecuted = DateTime.Now
                };

                _dbContext.TransactionRecords.Add(transactionRecord);
                await _dbContext.SaveChangesAsync();

                TempData["Message"] = "Success with transferring the funds.";
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }
            
            return RedirectToAction("Index");
        }

        // POST: /Manage/EnableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTwoFactorAuthentication()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!user.EmailConfirmed)
            {
                TempData["2FAMessage"] = "Email must be confirmed for 2FA.";
                return RedirectToAction("Index", "Manage");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest("Error generating 2FA token.");
            }

            await _signInManager.RefreshSignInAsync(user);

            return RedirectToAction("Index", "Manage");
        }

        // POST: /Manage/DisableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactorAuthentication()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user))
            {
                return BadRequest("2FA is not enabled for this user.");
            }

            var disable2FAResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
            if (!disable2FAResult.Succeeded)
            {
                return BadRequest("Error disabling 2FA.");
            }

            _logger.LogInformation("User with ID '{UserId}' has disabled 2FA.", user.Id);

            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction("Index", "Manage");
        }

        // GET: /Manage/ChangeEmail
        public IActionResult ChangeEmail()
        {
            return View();
        }

        // POST: /Manage/ChangeEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            User user = await GetCurrentUserAsync(GetCurrentUserId());

            if (user.Email.ToLowerInvariant() != model.OldEmail.ToLowerInvariant())   // ASP.NET Identity uses case-sensitivity with its fields for its DBContext and management
            {
                ModelState.AddModelError(string.Empty, "Old email doesn't match the current one.");
                return View(model);
            }

            user.Email = model.NewEmail;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangeEmailSuccess });
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Manage/ChangeUsername
        public IActionResult ChangeUsername()
        {
            return View();
        }

        // POST: /Manage/ChangeUsername
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUsername(ChangeUsernameViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            User user = await GetCurrentUserAsync(GetCurrentUserId());

            if (user.UserName != model.OldUsername)
            {
                ModelState.AddModelError(string.Empty, "Old username doesn't match the current one.");
                return View(model);
            }

            user.UserName = model.NewUsername;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangeUsernameSuccess });
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Manage/ChangePassword
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            User user = await GetCurrentUserAsync(GetCurrentUserId());

            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
                }

                AddErrors(result);
            }

            return View(model);
        }
    }
}