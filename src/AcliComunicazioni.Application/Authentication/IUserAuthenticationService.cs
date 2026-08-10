namespace AcliComunicazioni.Application.Authentication;

public interface IUserAuthenticationService
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticatedUser?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default);
}
