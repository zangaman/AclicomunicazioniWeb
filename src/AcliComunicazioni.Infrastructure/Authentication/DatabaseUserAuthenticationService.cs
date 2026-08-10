using System.Data;
using AcliComunicazioni.Application.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AcliComunicazioni.Infrastructure.Authentication;

public sealed class DatabaseUserAuthenticationService
    : IUserAuthenticationService
{
    private readonly string _connectionString;

    public DatabaseUserAuthenticationService(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' non configurata.");
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrEmpty(password))
        {
            return null;
        }

        var userRecord =
            await FindUserRecordAsync(username, cancellationToken);

        if (userRecord is null ||
            !string.Equals(
                userRecord.Password,
                password,
                StringComparison.Ordinal))
        {
            return null;
        }

        return ToAuthenticatedUser(userRecord);
    }

    public async Task<AuthenticatedUser?> FindByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var userRecord =
            await FindUserRecordAsync(username, cancellationToken);

        return userRecord is null
            ? null
            : ToAuthenticatedUser(userRecord);
    }

    private async Task<UserRecord?> FindUserRecordAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var suppliedUsername = username.Trim();
        var shortUsername = GetShortUsername(suppliedUsername);

        const string sql = """
            SELECT TOP (1)
                ID_utente,
                Utente,
                [Password],
                Nome,
                Cognome,
                Permessi_CE,
                Bloccato
            FROM dbo.Utenti
            WHERE Utente = @SuppliedUsername
               OR Utente = @ShortUsername
            ORDER BY
                CASE WHEN Utente = @SuppliedUsername THEN 0 ELSE 1 END;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(
            new SqlParameter("@SuppliedUsername", SqlDbType.NVarChar, 255)
            {
                Value = suppliedUsername
            });
        command.Parameters.Add(
            new SqlParameter("@ShortUsername", SqlDbType.NVarChar, 255)
            {
                Value = shortUsername
            });

        await using var reader =
            await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken) ||
            ReadBoolean(reader, "Bloccato"))
        {
            return null;
        }

        return new UserRecord(
            Id: Convert.ToInt32(reader["ID_utente"]),
            Username:
                Convert.ToString(reader["Utente"])
                ?? shortUsername,
            Password:
                reader["Password"] as string
                ?? string.Empty,
            FirstName:
                reader["Nome"] as string
                ?? string.Empty,
            LastName:
                reader["Cognome"] as string
                ?? string.Empty,
            PermissionLevel:
                reader["Permessi_CE"] is DBNull
                    ? 0
                    : Convert.ToInt32(reader["Permessi_CE"]));
    }

    private static AuthenticatedUser ToAuthenticatedUser(
        UserRecord userRecord)
    {
        var displayName =
            string.Join(
                " ",
                new[]
                {
                    userRecord.FirstName.Trim(),
                    userRecord.LastName.Trim()
                }.Where(value =>
                    !string.IsNullOrWhiteSpace(value)));

        return new AuthenticatedUser(
            Id: userRecord.Id,
            Username: userRecord.Username,
            DisplayName:
                string.IsNullOrWhiteSpace(displayName)
                    ? userRecord.Username
                    : displayName,
            Role:
                $"PermissionLevel:{userRecord.PermissionLevel}");
    }

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

    private static bool ReadBoolean(
        SqlDataReader reader,
        string columnName)
    {
        var value = reader[columnName];

        if (value is DBNull)
        {
            return false;
        }

        return value switch
        {
            bool booleanValue => booleanValue,
            byte byteValue => byteValue != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            string stringValue when bool.TryParse(
                stringValue,
                out var parsedBoolean) => parsedBoolean,
            string stringValue when int.TryParse(
                stringValue,
                out var parsedInteger) => parsedInteger != 0,
            _ => Convert.ToBoolean(value)
        };
    }

    private sealed record UserRecord(
        int Id,
        string Username,
        string Password,
        string FirstName,
        string LastName,
        int PermissionLevel);
}
