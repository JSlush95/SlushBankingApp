# Slush Banking

Slush Banking is an ASP.NET 8 (Core) MVC application that simulates an online banking system, featuring user account management, transaction functionality, and a secure integration with an interactive banking-related web API used by the [companion application](https://github.com/JSlush95/StorefrontApp). This interaction is for payment and refund transactions. This application leverages ASP.NET Identity for user authentication and authorization, and Entity Framework Core for data access.

## Table of Contents

- [Features](#features)
- [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Installation](#installation)
    - [Configuration](#configuration)
- [Usage](#usage)
    - [Bank API Integration](#bank-api-integration)
    - [User Account Management (Account Controller)](#user-account-management-account-controller)
    - [Bank Account Management (Manage Controller)](#bank-account-management-manage-controller)
    - [Bank Transaction Management (BankAPIController)](#bank-transaction-management-bankapicontroller)

## Features

- User Registration and Authentication with ASP.NET Identity Core
- Data Management with Entity Framework Core
- Account Management
- Transaction Processing
- Secure Payment and Refund Handling via Authorization Protocols and a Web API Controller
- Two-Factor Authentication (2FA)
- Email Confirmation for Accounts

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)
   - Note: this is for LocalDB testing, which this project is configured to. **Visual Studio** implements SQL Server natively. Alternative database implementation will require a different access provider in ``ApplicationDBContext.cs``. Entity Framework has some providers available. For example, Postgresql has a npgsql EF provider, which will use different database interaction syntax than what was used originally, requiring a possible migration schema change.

### Configuration - C# .NET 8 (.NET Core) - Program Variables

In .NET Core, you can configure the connection to a LocalDB SQL Server using a local JSON file (`appsettings.json`). Here’s how to set it up.<br>

1. Inspect the ``Program.cs`` file to see the switch case and how it references the provider cases, or add your own. The ``.json`` file configuration is used by default. The ``Program.cs`` will be the entry point to initializing services and ``.json`` references, so make sure everything is copied properly.

2. **Verify `appsettings.json` contents:**

    Ensure the connection string and provider for the LocalDB SQL Server is present in the `appsettings.json` or the upcoming section's ``secrets.json`` file:
    ```
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BankingAppDb;Trusted_Connection=True;MultipleActiveResultSets=true"
      }
    ```
    
    **Requires a secondary external ``secrets.json`` file**<br>
    Make sure to generate the private key (the banking website uses this private key):
    - Note: Change the "SupabaseConnection" string's key and value to whatever your PostgresQL server may be. Otherwise, remove its references in this file and ``Program.cs``'s switch case. Tailor this to your needs.
    
    ```{
      "AppSettings": {
        "MailAccount": "...",     // The Email Sender such as name@gmail.com
        "MailPassword": "...",     // The Email Password
        "SmtpHost": "...",       // Smtp Email Account such as smtp.gmail.com
        "PrivateKey": "..."  // Private key to encrypt data sent to the Bank API
      },
      "ConnectionStrings": {
        "SupabaseConnection": "Host=...;Database=...;Username=...;Password=...;"
      },
      "DatabaseProvider": "Postgresql"
        }
    ```
    
    Example of how I generated a cryptographic pair of private and public keys:
    ```
    RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048);
    
    // RSA keys in XML format
    string publicPrivateKeyXML = rsa.ToXmlString(true);
    string publicOnlyKeyXML = rsa.ToXmlString(false);
    
    // Export to file, etc
    ```

2. **Verify the DbContext Content:**

    2. **Verify the DbContext Content:**

    In your `Startup.cs` or `Program.cs` file, verify that the DbContext references the connection string from `appsettings.json`.<br>
    Default example for ``Startup.cs`` (older versions):
    ```
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
        
        // Other service configurations...
    }
    ```
    This program's `Program.cs` file:
    ```
    // Dynamically choose the provider for multiple providers and their migrations for EF Core
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        switch (provider)
        {
            case "SqlServer":
                options.UseSqlServer(connectionString);
                break;
            case "Postgresql":
                options.UseNpgsql(supabaseConnectionString);
                break;
            default:
                throw new InvalidOperationException($"Unsupported provider: {provider}");
        }
    });
    ```
    In Visual Studio, you can create an SQL server manually in the SQL Server Object Explorer, then you may use that database name instead.
3. **Apply Migrations:**

    After configuring the connection string, apply the migrations to set up the database schema using either the .NET CLI or Package Manager Console:

    **Using .NET CLI:**

    ```
    dotnet ef migrations add InitialCreate
    dotnet ef database update
    ```

    **Using Package Manager Console:**

    ```
    Add-Migration InitialCreate
    Update-Database
    ```

### Installation

1. Clone the repository:
    ```
    git clone https://github.com/JSlush95/SlushBankingApp.git
    cd BankingAppCore
    ```

2. Restore .NET dependencies:
    ```
    dotnet restore
    ```

3. Build the project:
    ```
    dotnet build
    ```

## Usage

### Bank API Integration

Utilizing asymmetric encryption with a confidential private key, I can keep the data secure while interacting with any that use this banking app's web API (namely the [companion application](https://github.com/JSlush95/StorefrontApp).

### User Account Management (Account Controller)
The application provides comprehensive user account management features, including registration, login, password recovery, and profile updates. It uses ASP.NET Identity for authentication and authorization, which allows more secure management.

#### Dependencies
The Account Controller uses the following services:
- `SignInManager<User>`
- `UserManager<User>`
- `ILogger<ManageController>`
- `IEmailSender`

**Relevant Functions:**
- **Register**: 
    - Method: POST
    - URL: `/Account/Register`
    - Body: `{ "username": "string", "password": "string", "email": "string" }`
    - **Description**: Registers a new user account with the provided username, password, and email.

- **Login**: 
    - Method: POST
    - URL: `/Account/Login`
    - Body: `{ "username": "string", "password": "string" }`
    - **Description**: Authenticates the user with the provided username and password, creating a session for the user.

- **Logout**: 
    - Method: POST
    - URL: `/Account/LogOff`
    - **Description**: Logs the user out of the application, ending their session.

- **ForgotPassword**:
    - Method: POST
    - URL: `/Account/ForgotPassword`
    - Body: `{ "email": "string" }`
    - **Description**: Initiates the password recovery process by sending a password reset email to the provided email address.

- **ResetPassword**:
    - Method: POST
    - URL: `/Account/ResetPassword`
    - Body: `{ "Email": "string", "Password": "string", "ConfirmPassword": "string", "Code": "string" }`
    - **Description**: Resets the user's password using the provided email, new password, confirmation password, and reset code.

- **SendCode**:
    - Method: POST
    - URL: `/Account/SendCode`
    - Body: `{ "Provider": "string", "ReturnUrl": "string", "RememberMe": true }`
    - **Description**: Sends a verification code for two-factor authentication or other security checks.

### Bank Account Management (Manage Controller)
This controller will handle matters related to bank accounts, such as payment methods, initiating transactions via the banking web API, and management of user accounts.

#### Dependencies

The Manage Controller uses the following services:
- `SignInManager<User>`
- `UserManager<User>`
- `ApplicationDbContext`
- `ILogger<ManageController>`
- `IEmailSender`

**Relevant Functions:**
- **SetAccountAlias**:
    - Method: POST
    - URL: `/Manage/SetAccountAlias`
    - Body: `{ "aliasFormInput": "string" }`
    - **Description**: Sets a new alias for the user's account.

- **CreateAccount**:
    - Method: POST
    - URL: `/Manage/CreateAccount`
    - Body: `{ "Balance": "decimal", "AccountType": "string" }`
    - **Description**: Creates a new bank account for the user.

- **AddFunds**:
    - Method: POST
    - URL: `/Manage/AddFunds`
    - Body: `{ "accountID": "int", "amount": "decimal" }`
    - **Description**: Adds funds to an existing bank account.

- **RemoveAccount**:
    - Method: POST
    - URL: `/Manage/RemoveAccount`
    - Body: `{ "accountID": "int" }`
    - **Description**: Removes a bank account from the user's profile.

- **CreateCard**:
    - Method: POST
    - URL: `/Manage/CreateCard`
    - Body: `{ "CardType": "string", "KeyPIN": "string", "SelectedAccountID": "int" }`
    - **Description**: Creates a new card for the user.

- **RemoveCard**:
    - Method: POST
    - URL: `/Manage/RemoveCard`
    - Body: `{ "cardID": "int" }`
    - **Description**: Removes a card from the user's profile.

- **TransferFunds**:
    - Method: POST
    - URL: `/Manage/TransferFunds`
    - Body: `{ "SourceAccountId": "int", "DestinationAccountId": "int", "Amount": "decimal" }`
    - **Description**: Transfers funds between two bank accounts.

- **EnableTwoFactorAuthentication**:
    - Method: POST
    - URL: `/Manage/EnableTwoFactorAuthentication`
    - **Description**: Enables two-factor authentication for the user.

- **DisableTwoFactorAuthentication**:
    - Method: POST
    - URL: `/Manage/DisableTwoFactorAuthentication`
    - **Description**: Disables two-factor authentication for the user.

- **ChangeEmail**:
    - Method: POST
    - URL: `/Manage/ChangeEmail`
    - Body: `{ "OldEmail": "string", "NewEmail": "string" }`
    - **Description**: Changes the user's email address.

- **ChangeUsername**:
    - Method: POST
    - URL: `/Manage/ChangeUsername`
    - Body: `{ "OldUsername": "string", "NewUsername": "string" }`
    - **Description**: Changes the user's username.

- **ChangePassword**:
    - Method: POST
    - URL: `/Manage/ChangePassword`
    - Body: `{ "OldPassword": "string", "NewPassword": "string", "ConfirmPassword": "string" }`
    - **Description**: Changes the user's password.

### Bank Transaction Management (BankAPIController)
This controller manages interactions with the external banking web API, including processing payments and handling refunds.

#### Dependencies

The Manage Controller uses the following services:
- `ApplicationDbContext`
- `Cryptography`
- `ILogger<ManageController>`

**Relevant Functions:**
- **InitiateTransaction**: 
    - Method: POST
    - URL: `/Manage/InitiateTransaction`
    - Body: `{ "EncryptedCardNumber": "string", "EncryptedKeyPIN": "string", "VendorTransactions": [{ "VendorAlias": "string", "TotalAmount": 0.0 }] }`
    - **Description**: Initiates a financial transaction by decrypting the provided card details, validating card status, checking account balances, and processing the transaction. The transaction is logged and certificates are generated for successful operations, these should be collected to use the ``IntiateRefund`` API.

- **InitiateRefund**:
    - Method: POST
    - URL: `/Manage/InitiateRefund`
    - Body: `{ "Certificates": ["string"], "Amounts": [0.0] }`
    - **Description**: Processes a refund by validating the provided certificate(s), checking if the original transaction exists and matches the user's alias, updating account balances, and logging the refund. A new refund transaction record is created and saved.
