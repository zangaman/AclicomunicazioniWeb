using AcliComunicazioni.Application.Authentication;
using Microsoft.Extensions.Options;

namespace AcliComunicazioni.Infrastructure.Authentication;

public sealed class DevelopmentUserAuthenticationService
    : IUserAuthenticationService
{
    private readonly DevelopmentAuthenticationOptions _options;

    public DevelopmentUserAuthenticationService(
        IOptions<DevelopmentAuthenticationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password) ||
            !UsernameMatches(username) ||
            !string.Equals(
                password,
                _options.Password,
                StringComparison.Ordinal))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        return Task.FromResult<AuthenticatedUser?>(
            CreateAuthenticatedUser());
    }

    public Task<AuthenticatedUser?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(username) ||
            !UsernameMatches(username))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        return Task.FromResult<AuthenticatedUser?>(
            CreateAuthenticatedUser());
    }

    private bool UsernameMatches(string username)
    {
        var suppliedUsername = username.Trim();
        var shortUsername = GetShortUsername(suppliedUsername);

        return string.Equals(
                   suppliedUsername,
                   _options.Username,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   shortUsername,
                   _options.Username,
                   StringComparison.OrdinalIgnoreCase);
    }

    private AuthenticatedUser CreateAuthenticatedUser() =>
        new(
            Id: _options.UserId,
            Username: _options.Username,
            DisplayName: _options.DisplayName,
            Role: _options.Role);

    private static string GetShortUsername(string username)
    {
        var slashPosition =
            Math.Max(
                username.LastIndexOf('\\'),
                username.LastIndexOf('/'));

        if (slashPosition >= 0 &&
            slashPosition < username.Length - 1)
        {
            return username[(slashPosition + 1)..];
        }

        var atPosition = username.IndexOf('@');

        return atPosition > 0
            ? username[..atPosition]
            : username;
    }
}
