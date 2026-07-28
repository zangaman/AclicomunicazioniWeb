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
            string.IsNullOrEmpty(password))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var usernameIsValid = string.Equals(
            username.Trim(),
            _options.Username,
            StringComparison.OrdinalIgnoreCase);

        var passwordIsValid = string.Equals(
            password,
            _options.Password,
            StringComparison.Ordinal);

        if (!usernameIsValid || !passwordIsValid)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var user = new AuthenticatedUser(
            Id: _options.UserId,
            Username: _options.Username,
            DisplayName: _options.DisplayName,
            Role: _options.Role);

        return Task.FromResult<AuthenticatedUser?>(user);
    }
}
