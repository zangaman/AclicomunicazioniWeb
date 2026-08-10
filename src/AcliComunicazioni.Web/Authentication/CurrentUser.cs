using AcliComunicazioni.Application.Authentication;
using AcliComunicazioni.Application.Common.Interfaces;
using System.Security.Claims;

namespace AcliComunicazioni.Web.Authentication;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal =>
        _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated =>
        Principal?.Identity?.IsAuthenticated ?? false;

    public int? UserId
    {
        get
        {
            var value =
                Principal?.FindFirstValue(
                    ApplicationClaimTypes.UserId)
                ?? Principal?.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }

    public string? UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Principal?.Identity?.Name;

    public string? DisplayName =>
        Principal?.FindFirstValue(
            ApplicationClaimTypes.DisplayName)
        ?? UserName;

    public string? Role =>
        Principal?.FindFirstValue(
            ApplicationClaimTypes.Role)
        ?? Principal?.FindFirstValue(
            ClaimTypes.Role);
}
