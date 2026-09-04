using System.Security.Claims;
using AcliComunicazioni.Application.Authentication;
using AcliComunicazioni.Web.Models.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcliComunicazioni.Web.Controllers;

public sealed class AccountController(
    IUserAuthenticationService authenticationService,
    IDomainCredentialValidator domainCredentialValidator,
    IConfiguration configuration,
    IWebHostEnvironment environment) : Controller
{
    private bool UsesDomainAuthentication() =>
        string.Equals(
            configuration["Authentication:Mode"],
            "Domain",
            StringComparison.OrdinalIgnoreCase);

    private bool UsesDomainCredentialLogin() =>
        UsesDomainAuthentication() &&
        configuration.GetValue<bool>(
            "Authentication:ActiveDirectory:Enabled");

    private bool UsesDevelopmentDualLogin() =>
        environment.IsDevelopment();

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(CreateLoginModel(returnUrl));
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        ApplyLoginOptions(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AuthenticatedUser? authenticatedUser;

        if (UsesDomainAuthentication() || UsesDevelopmentDualLogin())
        {
            if (!configuration.GetValue<bool>(
                    "Authentication:ActiveDirectory:Enabled") ||
                !await domainCredentialValidator.ValidateAsync(
                    model.Username,
                    model.Password,
                    cancellationToken))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Nome utente o password di dominio non validi.");
                return View(model);
            }

            authenticatedUser =
                await authenticationService.FindByUsernameAsync(
                    model.Username,
                    cancellationToken);
        }
        else
        {
            authenticatedUser =
                await authenticationService.AuthenticateAsync(
                    model.Username,
                    model.Password,
                    cancellationToken);
        }

        if (authenticatedUser is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Accesso non autorizzato.");
            return View(model);
        }

        await SignInApplicationUserAsync(
            authenticatedUser,
            model.RememberMe);

        return RedirectAfterLogin(model.ReturnUrl);
    }

    [Authorize(
        AuthenticationSchemes =
            NegotiateDefaults.AuthenticationScheme)]
    [HttpGet]
    public async Task<IActionResult> WindowsLogin(
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!UsesDomainAuthentication() && !UsesDevelopmentDualLogin())
        {
            return RedirectToAction(
                nameof(Login),
                new { returnUrl });
        }

        var windowsUsername = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(windowsUsername))
        {
            return RedirectToAction(nameof(AccessDenied));
        }

        var authenticatedUser =
            await authenticationService.FindByUsernameAsync(
                windowsUsername,
                cancellationToken);

        if (authenticatedUser is null)
        {
            return RedirectToAction(nameof(AccessDenied));
        }

        await SignInApplicationUserAsync(
            authenticatedUser,
            isPersistent: false);

        return RedirectAfterLogin(returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private LoginViewModel CreateLoginModel(string? returnUrl)
    {
        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl
        };

        ApplyLoginOptions(model);
        return model;
    }

    private void ApplyLoginOptions(LoginViewModel model)
    {
        model.ShowDualLoginButtons = UsesDevelopmentDualLogin();
        model.ShowWindowsLogin =
            UsesDevelopmentDualLogin() || UsesDomainAuthentication();
        model.UsesDomainCredentials =
            UsesDevelopmentDualLogin() || UsesDomainCredentialLogin();
    }

    private async Task SignInApplicationUserAsync(
        AuthenticatedUser authenticatedUser,
        bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                authenticatedUser.Id.ToString()),
            new(
                ApplicationClaimTypes.UserId,
                authenticatedUser.Id.ToString()),
            new(
                ClaimTypes.Name,
                authenticatedUser.Username),
            new(
                ApplicationClaimTypes.DisplayName,
                authenticatedUser.DisplayName),
            new(
                ApplicationClaimTypes.Role,
                authenticatedUser.Role),
            new(
                ClaimTypes.Role,
                authenticatedUser.Role)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(
                    isPersistent ? 12 : 2)
            });
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
