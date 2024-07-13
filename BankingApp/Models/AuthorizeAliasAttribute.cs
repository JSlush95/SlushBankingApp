using BankingApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Threading;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Net.Http;
using BankingApp.Utilities;

public class AuthorizeAliasAttribute : AuthorizationFilterAttribute
{
    private ApplicationDbContext _dbContext;
    private readonly Cryptography _cryptography;

    public AuthorizeAliasAttribute()
    {
        _cryptography = new Cryptography();
        _dbContext = new ApplicationDbContext();
    }

    public override async Task OnAuthorizationAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
    {
        var request = actionContext.Request;
        Log.Info("Beginning authorization from external API request.");

        try
        {
            if (request.Headers.Authorization == null || request.Headers.Authorization.Scheme != "Alias")
            {
                Log.Warn("Authorization header is missing or incorrect.");
                actionContext.Response = request.CreateResponse(HttpStatusCode.Unauthorized, "Authorization header is missing or incorrect.");
                return;
            }

            var encryptedAlias = request.Headers.Authorization.Parameter;
            var decryptedAlias = _cryptography.DecryptItem(encryptedAlias);
            decryptedAlias = GetAlias(decryptedAlias);

            if (string.IsNullOrEmpty(decryptedAlias))
            {
                HandleUnauthorizedRequest(actionContext, "Invalid alias used.", HttpStatusCode.Unauthorized);
                Log.Warn($"Invalid alias used.");
                return;
            }

            Log.Info($"Authorization successful for alias.");

            // Continue with the request pipeline.
            await base.OnAuthorizationAsync(actionContext, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error("An error occurred during authorization.", ex);
            actionContext.Response = request.CreateResponse(HttpStatusCode.InternalServerError, "An internal error occurred.");
        }
    }

    private void HandleUnauthorizedRequest(HttpActionContext actionContext, string errorMessage, HttpStatusCode statusCode)
    {
        Log.Warn("Creating the unauthorized response for the context...");

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($"<html><body><h1>{(int)statusCode} {statusCode}</h1><p>{errorMessage}</p></body></html>")
        };
        
        actionContext.Response = response;
    }

    private string GetAlias(string alias)
    {
        string selectedAlias = _dbContext.Users
            .Where(u => u.Alias == alias)
            .Select(u => u.Alias)
            .FirstOrDefault();

        return selectedAlias;
    }
}