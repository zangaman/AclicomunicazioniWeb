namespace AcliComunicazioni.Application.Authentication;

public interface IDomainCredentialValidator
{
    Task<bool> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
