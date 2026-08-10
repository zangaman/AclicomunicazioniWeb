using System.Security.Claims;
using AcliComunicazioni.Application.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;

namespace AcliComunicazioni.Web.Authentication;

public sealed class DomainUserClaimsTransformation(
    IUserAuthenticationService userAuthenticationService,
    IMemoryCache cache)
    : IClaimsTransformation
{
    private static readonly TimeSpan CacheDuration =
        TimeSpan.FromMinutes(5);

    public async Task<ClaimsPrincipal> TransformAsync(
        ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            principal.HasClaim(
                claim =>
                    claim.Type == ApplicationClaimTypes.UserId))
        {
            return principal;
        }

        var domainUsername = principal.Identity.Name;

        if (string.IsNullOrWhiteSpace(domainUsername))
        {
            return principal;
        }

        var cacheKey =
            $"domain-user:{domainUsername.ToUpperInvariant()}";

        if (!cache.TryGetValue(
                cacheKey,
                out AuthenticatedUser? applicationUser))
        {
            applicationUser =
                await userAuthenticationService.FindByUsernameAsync(
                    domainUsername);

            cache.Set(
                cacheKey,
                applicationUser,
                CacheDuration);
        }

        if (applicationUser is null ||
            principal.Identity is not ClaimsIdentity identity)
        {
            return principal;
        }

        identity.AddClaims(
        [
            new Claim(
                ApplicationClaimTypes.UserId,
                applicationUser.Id.ToString()),
            new Claim(
                ApplicationClaimTypes.DisplayName,
                applicationUser.DisplayName),
            new Claim(
                ApplicationClaimTypes.Role,
                applicationUser.Role),
            new Claim(
                ClaimTypes.Role,
                applicationUser.Role)
        ]);

        return principal;
    }
}
