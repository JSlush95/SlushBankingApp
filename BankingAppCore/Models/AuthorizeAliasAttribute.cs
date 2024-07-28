using BankingAppCore.Models;
using BankingAppCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

public class AuthorizeAliasRequirement : IAuthorizationRequirement { }

public class AuthorizeAliasHandler : AuthorizationHandler<AuthorizeAliasRequirement>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Cryptography _cryptography;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthorizeAliasHandler> _logger;

    public AuthorizeAliasHandler(ApplicationDbContext dbContext, Cryptography cryptography, IHttpContextAccessor httpContextAccessor, ILogger<AuthorizeAliasHandler> logger)
    {
        _dbContext = dbContext;
        _cryptography = cryptography;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AuthorizeAliasRequirement requirement)
    {
        var request = _httpContextAccessor.HttpContext.Request;
        _logger.LogInformation("Beginning authorization from external API request.");

        try
        {
            if (!request.Headers.ContainsKey("Authorization"))
            {
                _logger.LogWarning("Authorization header is missing.");
                context.Fail();
                return Task.CompletedTask;
            }

            var authorizationHeader = request.Headers["Authorization"].ToString();
            if (!authorizationHeader.StartsWith("Alias ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Authorization header is incorrect.");
                context.Fail();
                return Task.CompletedTask;
            }

            var encryptedAlias = authorizationHeader.Substring("Alias ".Length).Trim();
            var decryptedAlias = _cryptography.DecryptItem(encryptedAlias);
            decryptedAlias = GetAlias(decryptedAlias);

            if (string.IsNullOrEmpty(decryptedAlias))
            {
                _logger.LogWarning("Invalid alias used.");
                context.Fail();
                return Task.CompletedTask;
            }

            _logger.LogInformation("Authorization successful for alias.");
            context.Succeed(requirement);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "An error occurred during decryption.");
            context.Fail();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during authorization.");
            context.Fail();
        }

        return Task.CompletedTask;
    }

    private string GetAlias(string alias)
    {
        return _dbContext.Users
            .Where(u => u.Alias == alias)
            .Select(u => u.Alias)
            .FirstOrDefault();
    }
}
