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
            WHERE Utente = @Username;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(
            new SqlParameter("@Username", SqlDbType.NVarChar, 255)
            {
                Value = username.Trim()
            });

        await using var reader =
            await command.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var isBlocked = ReadBoolean(reader, "Bloccato");

        if (isBlocked)
        {
            return null;
        }

        var storedPassword =
            reader["Password"] as string ?? string.Empty;

        // Prima versione richiesta: confronto diretto con il valore esistente
        // nella tabella dbo.Utenti. In seguito potrà essere sostituito con
        // hashing o con l'algoritmo usato dal vecchio gestionale.
        if (!string.Equals(
                storedPassword,
                password,
                StringComparison.Ordinal))
        {
            return null;
        }

        var firstName =
            reader["Nome"] as string ?? string.Empty;

        var lastName =
            reader["Cognome"] as string ?? string.Empty;

        var displayName =
            string.Join(
                " ",
                new[] { firstName.Trim(), lastName.Trim() }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

        var permissionLevel =
            reader["Permessi_CE"] is DBNull
                ? 0
                : Convert.ToInt32(reader["Permessi_CE"]);

        return new AuthenticatedUser(
            Id: Convert.ToInt32(reader["ID_utente"]),
            Username: Convert.ToString(reader["Utente"]) ?? username.Trim(),
            DisplayName: string.IsNullOrWhiteSpace(displayName)
                ? username.Trim()
                : displayName,
            Role: $"PermissionLevel:{permissionLevel}");
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
}
