using AcliComunicazioni.Application.Authentication;
using AcliComunicazioni.Application.Administration;
using AcliComunicazioni.Application.Common.Interfaces;
using AcliComunicazioni.Application.Mileage;
using AcliComunicazioni.Infrastructure.Authentication;
using AcliComunicazioni.Infrastructure.Administration;
using AcliComunicazioni.Infrastructure.Mileage;
using AcliComunicazioni.Web.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "AcliComunicazioni.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IUserAuthenticationService,
    DatabaseUserAuthenticationService>();
builder.Services.AddScoped<IAdministrationService, DatabaseAdministrationService>();

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
