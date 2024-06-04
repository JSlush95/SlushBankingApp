using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using BankingApp.Models;
using System.Data.Entity;
using System.Collections.Generic;

namespace BankingApp.Controllers
{
    [Authorize]
    public class ManageController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationDbContext _dbContext;

        public ManageController()
            :this(new ApplicationDbContext(), null, null)
        {
        }

        public ManageController(ApplicationDbContext context, ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            ContextDbManager = context;
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationDbContext ContextDbManager
        {
            get
            {
                return _dbContext ?? HttpContext.GetOwinContext().Get<ApplicationDbContext>();
            }
            private set
            {
                _dbContext = value;
            }
        }

        public ApplicationSignInManager SignInManager
        {
            get
            {
                return _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            }
            private set 
            { 
                _signInManager = value; 
            }
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        private int GetCurrentUserId()
        {
            return User.Identity.GetUserId<int>();
        }

        private async Task<IndexViewModel> CreateIndexViewModel()
        {
            var userId = GetCurrentUserId();
            var user = await UserManager.FindByIdAsync(userId);
            var indexModel = new IndexViewModel
            {
                HasPassword = HasPassword(),
                PhoneNumber = await UserManager.GetPhoneNumberAsync(userId),
                TwoFactor = await UserManager.GetTwoFactorEnabledAsync(userId),
                Logins = await UserManager.GetLoginsAsync(userId),
                BrowserRemembered = await AuthenticationManager.TwoFactorBrowserRememberedAsync(User.Identity.GetUserId()),
                EmailConfirmed = user.EmailConfirmed
            };

            return indexModel;
        }

        //
        // GET: /Manage/Index
        public async Task<ActionResult> Index(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
                : message == ManageMessageId.ChangeUsernameSuccess ? "Your username has been changed."
                : message == ManageMessageId.ChangeEmailSuccess ? "Your email has been changed."
                : message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
                : message == ManageMessageId.SetTwoFactorSuccess ? "Your two-factor authentication provider has been set."
                : message == ManageMessageId.Error ? "An error has occurred."
                : message == ManageMessageId.AddPhoneSuccess ? "Your phone number was added."
                : message == ManageMessageId.RemovePhoneSuccess ? "Your phone number was removed."
                : "";

            int userId = GetCurrentUserId();    // Must get the user's ID field for the LINQ functionality, as it cannot evaluate the function call directly.
            
            var bankAccounts = await _dbContext.BankAccounts    // Creating LINQ queries that will be provided as helper functions to the respective partial views that use their models.
                .Where(ba => ba.Holder == userId)
                .ToListAsync();
            var cards = await _dbContext.Cards
                .Where(c => c.AssociatedBankAccount.Holder == userId)
                .ToListAsync();
            var transactions = await _dbContext.TransactionRecords
                .Where(t => t.SenderAccount.Holder == userId || t.RecipientAccount.Holder == userId)
                .ToListAsync();

            if (TempData.ContainsKey("Message"))
            {
                ViewBag.Message = TempData["Message"].ToString();
            }

            var model = await CreateIndexViewModel();
            model.CreateCardViewModel = new CreateCardViewModel
            {
                BankAccounts = bankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType}"
                }).ToList()
            };
            model.TransferFundsViewModel = new TransferFundsViewModel
            {
                SourceAccounts = bankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType} - Balance: {ba.Balance}"
                }),
                DestinationAccounts = bankAccounts.Select(ba => new SelectListItem
                {
                    Value = ba.AccountID.ToString(),
                    Text = $"Account ID: {ba.AccountID} - {ba.AccountType} - Balance: {ba.Balance}"
                })
            };
            model.DisplayAccountInfoViewModel = new DisplayAccountInfoViewModel
            {
                Accounts = bankAccounts,
                Cards = cards
            };
            model.DisplayTransferInfoViewModel = new DisplayTransferInfoViewModel
            {
                Transactions = transactions
            };

            return View(model);
        }

        //
        // POST: /Manage/RemoveLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveLogin(string loginProvider, string providerKey)
        {
            ManageMessageId? message;
            var result = await UserManager.RemoveLoginAsync(GetCurrentUserId(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(GetCurrentUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("ManageLogins", new { Message = message });
        }

        // POST: /Manage/CreateAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateAccount(CreateAccountViewModel model)
        {
            int userId = GetCurrentUserId();    // Must get the user's ID field for the LINQ functionality, as it cannot evaluate the function call directly.
            var user = await UserManager.FindByIdAsync(userId);
            var indexModel = await CreateIndexViewModel();
            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used when creating a new bank account, please follow the rules and try again.";
                return RedirectToAction("Index", indexModel);
            }

            if (user.EmailConfirmed)
            {
                // Utilizing LINQ to query the last time this element was last created, as the anchor to prevent spam.
                var lastAccountCreationTimestamp = await _dbContext.BankAccounts
                    .Where(a => a.Holder == userId)
                    .OrderByDescending(a => a.DateOpened)
                    .Select(a => a.DateOpened)
                    .FirstOrDefaultAsync();

                var newAccount = new BankAccount
                {
                    Balance = model.Balance,
                    Holder = GetCurrentUserId(),
                    DateOpened = DateTime.Now,
                    AccountType = model.AccountType
                };

                var cooldownPeriod = TimeSpan.FromMinutes(1);  // The cooldown period in minutes.

                // Creating a cooldown timer to prevent creation abuse.
                if (lastAccountCreationTimestamp != default && DateTime.Now - lastAccountCreationTimestamp < cooldownPeriod)
                {
                    var timeElapsed = DateTime.Now - lastAccountCreationTimestamp;
                    var timeRemaining = cooldownPeriod - timeElapsed;

                    if (timeRemaining > TimeSpan.Zero)
                    {
                        TempData["Message"] = $"Please wait {timeRemaining} before creating a new account.";
                        return RedirectToAction("Index", indexModel);
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
            
            return RedirectToAction("Index", indexModel);
        }

        // POST: /Manage/AddFunds
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddFunds(int accountID, decimal? amount)
        {
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            var indexModel = await CreateIndexViewModel();
            var account = await _dbContext.BankAccounts
                .Where(a => a.AccountID == accountID)
                .FirstOrDefaultAsync();
            
            if (user.EmailConfirmed)
            {
                if (!amount.HasValue || amount <= 0)
                {
                    TempData["Message"] = "Please enter a non-zero number amount.";
                }
                else if (account != null)
                {
                    var transaction = new TransactionRecord()
                    {
                        Sender = accountID,
                        Recipient = accountID,
                        Amount = (decimal)amount,
                        Description = "Deposit",
                        TimeExecuted = DateTime.Now,
                    };

                    using (var transactionContext = _dbContext.Database.BeginTransaction()) // Using a transaction scope to handle more than one change.
                    {
                        try
                        {
                            account.Balance += (decimal)amount;
                            _dbContext.TransactionRecords.Add(transaction);

                            await _dbContext.SaveChangesAsync();
                            transactionContext.Commit();

                            TempData["Message"] = $"Success with adding {amount} to the selected bank account.";
                        }
                        catch (Exception ex)
                        {
                            transactionContext.Rollback();
                            TempData["Message"] = "Failed to add funds to the bank account.";
                        }

                    }
                }
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }

            return RedirectToAction("Index", indexModel);
        }

        // POST: /Manage/RemoveAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveAccount(int accountID)
        {
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            var indexModel = await CreateIndexViewModel();
            var account = await _dbContext.BankAccounts
                .Where(a => a.AccountID == accountID)
                .FirstOrDefaultAsync();

            if (user.EmailConfirmed)
            {
                if(account != null)
                {
                    _dbContext.BankAccounts.Remove(account);
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
            
            return RedirectToAction("index", indexModel);
        }

        // POST: /Manage/CreateCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateCard(CreateCardViewModel model)
        {
            int userId = GetCurrentUserId();    // Must get the user's ID field for the LINQ functionality, as it cannot evaluate the function call directly.
            var user = await UserManager.FindByIdAsync(userId);
            var indexModel = await CreateIndexViewModel();

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used when creating a new card, please follow the rules and try again.";
                return RedirectToAction("Index", indexModel);
            }

            // Utilizing LINQ to query the last time this element was last created, as the anchor to prevent spam.
            var lastCardCreationTimestamp = await _dbContext.Cards
                .Where(a => a.AssociatedAccount == userId)
                .OrderByDescending(a => a.IssueDate)
                .Select(a => a.IssueDate)
                .FirstOrDefaultAsync();
            var cooldownPeriod = TimeSpan.FromMinutes(1);  // The cooldown period in minutes.

            // Creating a cooldown timer to prevent creation abuse.
            if (lastCardCreationTimestamp != default && DateTime.Now - lastCardCreationTimestamp < cooldownPeriod)
            {
                var timeElapsed = DateTime.Now - lastCardCreationTimestamp;
                var timeRemaining = cooldownPeriod - timeElapsed;

                if (timeRemaining > TimeSpan.Zero)
                {
                    TempData["Message"] = $"Please wait {timeRemaining} before creating a new card.";
                    return RedirectToAction("Index", indexModel);
                }
            }

            if (user.EmailConfirmed)
            {
                string generatedCardNumber;
                bool isUnique;
                string getUniqueString(int stringLength)
                {
                    using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
                    {
                        var bit_count = (stringLength * 6);
                        var byte_count = ((bit_count + 7) / 8); // Round up
                        var bytes = new byte[byte_count];
                        rng.GetBytes(bytes);
                        var base64String = Convert.ToBase64String(bytes);
                        return base64String.Substring(0, stringLength);
                    }
                }

                // Checking whether this card number exists in the database, because it would break the system, since cards cannot share their unique usage code.
                do
                {
                    generatedCardNumber = getUniqueString(11);  // Length - 1
                    isUnique = !_dbContext.Cards.Any(c => c.CardNumber == generatedCardNumber);
                }while(!isUnique && generatedCardNumber.Length != 11);

                var newCard = new Card
                {
                    CardType = model.CardType,
                    CardNumber = generatedCardNumber,
                    AssociatedAccount = model.SelectedAccountID,
                    IssueDate = DateTime.Now,
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
            
            return RedirectToAction("Index", indexModel);
        }

        // POST: /Manage/RemoveCard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveCard(int cardID)
        {
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            var indexModel = await CreateIndexViewModel();
            var card = _dbContext.Cards
                .Where(a => a.CardID == cardID)
                .FirstOrDefault();

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
            
            return RedirectToAction("Index", indexModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // POST: /Manage/TransferFunds
        public async Task<ActionResult> TransferFunds(TransferFundsViewModel model)
        {
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            var indexModel = await CreateIndexViewModel();
            var sourceAccount = await _dbContext.BankAccounts.FindAsync(model.SourceAccountId);
            var destinationAccount = await _dbContext.BankAccounts.FindAsync(model.DestinationAccountId);

            if (!ModelState.IsValid)
            {
                TempData["Message"] = "Invalid data used for transferring, please follow the rules and try again.";
                return RedirectToAction("Index", indexModel);
            }

            if (user.EmailConfirmed)
            {
                // Checking if both accounts exist and the source account has sufficient funds.
                if (sourceAccount == null || destinationAccount == null)
                {
                    TempData["Message"] = "One or both of the bank accounts do not exist.";
                    //ModelState.AddModelError("", "One or both of the bank accounts do not exist.");
                    return RedirectToAction("Index", indexModel);
                }

                if (sourceAccount.AccountID == destinationAccount.AccountID)
                {
                    TempData["Message"] = "Please choose different accounts to transfer funds.";
                    return RedirectToAction("Index", indexModel);
                }

                if (sourceAccount.Balance < model.Amount)
                {
                    // Handle the case where the source account does not have sufficient funds
                    TempData["Message"] = "Insufficient funds in the source account.";
                    //ModelState.AddModelError("", "Insufficient funds in the source account.");
                    return RedirectToAction("Index", indexModel);
                }

                destinationAccount.Balance += model.Amount;
                sourceAccount.Balance -= model.Amount;

                TransactionRecord transactionRecord = new TransactionRecord()
                {
                    Sender = sourceAccount.AccountID,
                    Recipient = destinationAccount.AccountID,
                    Amount = model.Amount,
                    Description = "Transfer",
                    TimeExecuted = DateTime.Now
                };

                _dbContext.TransactionRecords.Add(transactionRecord);
                await _dbContext.SaveChangesAsync();

                TempData["Message"] = "Success with transfering the funds.";
            }
            else
            {
                TempData["Message"] = "Please confirm your email first.";
            }
            
            return RedirectToAction("Index", indexModel);
        }

        //
        // GET: /Manage/AddPhoneNumber
        public ActionResult AddPhoneNumber()
        {
            return View();
        }

        //
        // POST: /Manage/AddPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddPhoneNumber(AddPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // Generate the token and send it
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(GetCurrentUserId(), model.Number);
            if (UserManager.SmsService != null)
            {
                var message = new IdentityMessage
                {
                    Destination = model.Number,
                    Body = "Your security code is: " + code
                };
                await UserManager.SmsService.SendAsync(message);
            }
            return RedirectToAction("VerifyPhoneNumber", new { PhoneNumber = model.Number });
        }

        //
        // POST: /Manage/EnableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EnableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(GetCurrentUserId(), true);
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // POST: /Manage/DisableTwoFactorAuthentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DisableTwoFactorAuthentication()
        {
            await UserManager.SetTwoFactorEnabledAsync(GetCurrentUserId(), false);
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", "Manage");
        }

        //
        // GET: /Manage/VerifyPhoneNumber
        public async Task<ActionResult> VerifyPhoneNumber(string phoneNumber)
        {
            var code = await UserManager.GenerateChangePhoneNumberTokenAsync(GetCurrentUserId(), phoneNumber);
            // Send an SMS through the SMS provider to verify the phone number
            return phoneNumber == null ? View("Error") : View(new VerifyPhoneNumberViewModel { PhoneNumber = phoneNumber });
        }

        //
        // POST: /Manage/VerifyPhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyPhoneNumber(VerifyPhoneNumberViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePhoneNumberAsync(GetCurrentUserId(), model.PhoneNumber, model.Code);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(GetCurrentUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.AddPhoneSuccess });
            }
            // If we got this far, something failed, redisplay form
            ModelState.AddModelError("", "Failed to verify phone");
            return View(model);
        }

        //
        // POST: /Manage/RemovePhoneNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemovePhoneNumber()
        {
            var result = await UserManager.SetPhoneNumberAsync(GetCurrentUserId(), null);
            if (!result.Succeeded)
            {
                return RedirectToAction("Index", new { Message = ManageMessageId.Error });
            }
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            if (user != null)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
            }
            return RedirectToAction("Index", new { Message = ManageMessageId.RemovePhoneSuccess });
        }

        // GET: /Manage/ChangeEmail
        public ActionResult ChangeEmail()
        {
            return View();
        }

        // POST: /Manage/ChangeEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeEmail(ChangeEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());

            if (user.Email.ToLowerInvariant() != model.OldEmail.ToLowerInvariant())   // ASP.NET Identity uses case-sensitivity with its fields for its DBContext and management, lower case the conditions.
            {
                ModelState.AddModelError(string.Empty, "Old email doesn't match the current one.");
                return View(model);
            }

            user.Email = model.NewEmail;
            var result = await UserManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangeEmailSuccess });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
            return View(model);
        }

        // GET: /Manage/ChangeUsername
        public ActionResult ChangeUsername()
        {
            return View();
        }

        // POST: /Manage/ChangeUsername
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeUsername(ChangeUsernameViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());

            if (user.UserName != model.OldUsername)
            {
                ModelState.AddModelError(string.Empty, "Old username doesn't match the current one.");
                return View(model);
            }

            user.UserName = model.NewUsername;
            var result = await UserManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangeUsernameSuccess });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
            return View(model);
        }

        //
        // GET: /Manage/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        //
        // POST: /Manage/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await UserManager.ChangePasswordAsync(GetCurrentUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(GetCurrentUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                return RedirectToAction("Index", new { Message = ManageMessageId.ChangePasswordSuccess });
            }
            AddErrors(result);
            return View(model);
        }

        //
        // GET: /Manage/SetPassword
        public ActionResult SetPassword()
        {
            return View();
        }

        //
        // POST: /Manage/SetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetPassword(SetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await UserManager.AddPasswordAsync(GetCurrentUserId(), model.NewPassword);
                if (result.Succeeded)
                {
                    var user = await UserManager.FindByIdAsync(GetCurrentUserId());
                    if (user != null)
                    {
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                    }
                    return RedirectToAction("Index", new { Message = ManageMessageId.SetPasswordSuccess });
                }
                AddErrors(result);
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // GET: /Manage/ManageLogins
        public async Task<ActionResult> ManageLogins(ManageMessageId? message)
        {
            ViewBag.StatusMessage =
                message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
                : message == ManageMessageId.Error ? "An error has occurred."
                : "";
            var user = await UserManager.FindByIdAsync(GetCurrentUserId());
            if (user == null)
            {
                return View("Error");
            }
            var userLogins = await UserManager.GetLoginsAsync(GetCurrentUserId());
            var otherLogins = AuthenticationManager.GetExternalAuthenticationTypes().Where(auth => userLogins.All(ul => auth.AuthenticationType != ul.LoginProvider)).ToList();
            ViewBag.ShowRemoveButton = user.PasswordHash != null || userLogins.Count > 1;
            return View(new ManageLoginsViewModel
            {
                CurrentLogins = userLogins,
                OtherLogins = otherLogins
            });
        }

        //
        // POST: /Manage/LinkLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new AccountController.ChallengeResult(provider, Url.Action("LinkLoginCallback", "Manage"), User.Identity.GetUserId());
        }

        //
        // GET: /Manage/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(GetCurrentUserId(), loginInfo.Login);
            return result.Succeeded ? RedirectToAction("ManageLogins") : RedirectToAction("ManageLogins", new { Message = ManageMessageId.Error });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }

#region Helpers
        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private bool HasUsername()
        {
            var user = UserManager.FindByName(User.Identity.Name);
            if (user != null)
            {
                return user != null;
            }
            return false;
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(GetCurrentUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        private bool HasPhoneNumber()
        {
            var user = UserManager.FindById(GetCurrentUserId());
            if (user != null)
            {
                return user.PhoneNumber != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            ChangeUsernameSuccess,
            ChangeEmailSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }

#endregion
    }
}