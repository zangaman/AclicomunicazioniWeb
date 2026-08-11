using AcliComunicazioni.Application.Authentication;
using AcliComunicazioni.Application.Common.Interfaces;
using AcliComunicazioni.Application.Mileage;
using AcliComunicazioni.Infrastructure.Authentication;
using AcliComunicazioni.Infrastructure.Mileage;
using AcliComunicazioni.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using PdfSharp.Fonts;
using System.Data.Common;

var builder = WebApplication.CreateBuilder(args);

ValidateEnvironmentConfiguration(
    builder.Environment,
    builder.Configuration);

var useDomainAuthentication =
    UsesDomainAuthentication(builder.Configuration);

if (OperatingSystem.IsWindows())
{
    GlobalFontSettings.UseWindowsFontsUnderWindows = true;
}

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var authenticationBuilder = builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "AcliComunicazioni.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;

        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

if (useDomainAuthentication)
{
    authenticationBuilder.AddNegotiate(
        NegotiateDefaults.AuthenticationScheme);
}

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IUserAuthenticationService,
    DatabaseUserAuthenticationService>();
builder.Services.AddScoped<
    IDomainCredentialValidator,
    ActiveDirectoryCredentialValidator>();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IMileageService, DatabaseMileageService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static bool UsesDomainAuthentication(
    IConfiguration configuration)
{
    var mode =
        configuration["Authentication:Mode"]?.Trim();

    return mode?.ToUpperInvariant() switch
    {
        "DOMAIN" => true,
        "LOCAL" => false,
        _ => throw new InvalidOperationException(
            "Authentication:Mode deve essere configurato " +
            "come 'Local' oppure 'Domain'.")
    };
}

static void ValidateEnvironmentConfiguration(
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    var expectedDatabase = environment.EnvironmentName switch
    {
        "Development" => "AcliComunicazioni_Sviluppo",
        "Test" => "AcliComunicazioni_Test",
        "Production" => "AcliComunicazioni_Produzione",
        _ => throw new InvalidOperationException(
            $"Ambiente '{environment.EnvironmentName}' non supportato. " +
            "Usare Development, Test oppure Production.")
    };

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            $"Connection string 'DefaultConnection' non configurata per l'ambiente {environment.EnvironmentName}.");
    }

    var connectionData = new DbConnectionStringBuilder
    {
        ConnectionString = connectionString
    };

    var configuredDatabase = connectionData.TryGetValue("Database", out var database)
        ? database?.ToString()
        : connectionData.TryGetValue("Initial Catalog", out var initialCatalog)
            ? initialCatalog?.ToString()
            : null;

    if (!string.Equals(configuredDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Configurazione bloccata: l'ambiente {environment.EnvironmentName} deve usare " +
            $"il database '{expectedDatabase}', non '{configuredDatabase ?? "non specificato"}'.");
    }
}
