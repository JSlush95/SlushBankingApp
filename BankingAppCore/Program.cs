using BankingAppCore.Data;
using BankingAppCore.Models;
using BankingAppCore.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NReco.Logging.File;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Reflection;
using BankingAppCore.Controllers;
using BankingAppCore.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var databaseConnection = Environment.GetEnvironmentVariable("CUSTOMCONNSTR_DBConnection") ?? throw new InvalidOperationException("Connection string base 'DBConnection' not found.");
var certificatePath = Environment.GetEnvironmentVariable("ROOT_CERTIFICATE_PATH") ?? throw new InvalidOperationException("Certificate path not found.");

// Combining with the web root path to get the absolute path
var rootCertificateAbsolutePath = Path.Combine(builder.Environment.WebRootPath, certificatePath);

// Validating the certificate path
if (!File.Exists(rootCertificateAbsolutePath))
{
    throw new FileNotFoundException("Root certificate file not found.", rootCertificateAbsolutePath);
}

var connectionString = databaseConnection + "SSLMode=Require;Trust Server Certificate=false;Root Certificate=" + rootCertificateAbsolutePath + ";Include Error Detail=true;";

// Binding the appsettings.json section to a POCO class
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Adding and configuring the Identity functionality
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    // Confirmation settings
    options.SignIn.RequireConfirmedAccount = false;

    // Two-Factor Authentication settings
    options.Tokens.AuthenticatorIssuer = "Slush Banking";
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;

    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Registering the authorization handler
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthorizeAliasPolicy", policy =>
        policy.Requirements.Add(new AuthorizeAliasRequirement()));
});

// Registering EmailService as the implementation of IEmailSender
builder.Services.AddTransient<IEmailSender, EmailService>();

// Add configuration from appsettings.json and secrets.json
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Registering the AuthorizeAliasHandler and Cryptography classes
builder.Services.AddScoped<IAuthorizationHandler, AuthorizeAliasHandler>();
builder.Services.AddScoped<Cryptography>();

// Registering the classes that will be used by the hosted services for periodic card/account checks
builder.Services.AddScoped<BankAccountService>();
builder.Services.AddScoped<CardService>();

// Registering the hosted services that will run as background services to periodically check savings accounts and cards
builder.Services.AddHostedService<SavingsAccountInterestHostedService>();
builder.Services.AddHostedService<CardExpireHostedService>();

// Adding a custom logger, the NReco.Logging.File Nuget package 
builder.Services.AddLogging(loggingBuilder => {
    var loggingSection = builder.Configuration.GetSection("Logging");
    loggingBuilder.AddFile(loggingSection);
});

// Adding the services for MVC and Razor syntax
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddControllers().AddApplicationPart(typeof(BankAPIController).Assembly);

// Configuring the application cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/LogOff";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Configuring the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
// Controllers are used for API Controller classes
app.MapControllers();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
