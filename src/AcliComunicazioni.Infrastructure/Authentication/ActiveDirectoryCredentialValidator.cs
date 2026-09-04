using System.DirectoryServices.Protocols;
using System.Net;
using AcliComunicazioni.Application.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AcliComunicazioni.Infrastructure.Authentication;

public sealed class ActiveDirectoryCredentialValidator(
    IConfiguration configuration,
    ILogger<ActiveDirectoryCredentialValidator> logger)
    : IDomainCredentialValidator
{
    public async Task<bool> ValidateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password) ||
            !configuration.GetValue<bool>(
                "Authentication:ActiveDirectory:Enabled"))
        {
            return false;
        }

        var server = configuration[
            "Authentication:ActiveDirectory:Server"]?.Trim();
        var suffix = configuration[
            "Authentication:ActiveDirectory:UserPrincipalSuffix"]?.Trim();
        var port = configuration.GetValue<int?>(
            "Authentication:ActiveDirectory:Port") ?? 389;
        var useSsl = configuration.GetValue<bool>(
            "Authentication:ActiveDirectory:UseSsl");

        if (string.IsNullOrWhiteSpace(server) ||
            string.IsNullOrWhiteSpace(suffix) ||
            port <= 0)
        {
            logger.LogError(
                "Configurazione Active Directory incompleta. " +
                "Sono obbligatori Server, UserPrincipalSuffix e una porta valida.");
            return false;
        }

        var bindUsername = NormalizeUsername(username, suffix);

        try
        {
            return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var identifier =
                        new LdapDirectoryIdentifier(server, port);

                    var authType = useSsl
                        ? AuthType.Basic
                        : AuthType.Negotiate;

                    using var connection =
                        new LdapConnection(
                            identifier,
                            new NetworkCredential(bindUsername, password),
                            authType)
                        {
                            Timeout = TimeSpan.FromSeconds(10)
                        };

                    connection.SessionOptions.ProtocolVersion = 3;
                    connection.SessionOptions.SecureSocketLayer = useSsl;

                    if (!useSsl)
                    {
                        connection.SessionOptions.Signing = true;
                        connection.SessionOptions.Sealing = true;
                    }

                    connection.Bind();

                    return true;
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LdapException exception)
        {
            logger.LogWarning(
                "Verifica Active Directory non riuscita. Codice LDAP: {ErrorCode}.",
                exception.ErrorCode);
            return false;
        }
    }

    private static string NormalizeUsername(
        string username,
        string suffix)
    {
        var normalized = username.Trim();

        return normalized.Contains('@') ||
               normalized.Contains('\\')
            ? normalized
            : $"{normalized}@{suffix}";
    }
}
