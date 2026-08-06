using System.Data;
using AcliComunicazioni.Application.Administration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AcliComunicazioni.Infrastructure.Administration;

public sealed class DatabaseAdministrationService : IAdministrationService
{
    private readonly string _connectionString;

    public DatabaseAdministrationService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' non configurata.");
    }

    public async Task<AdministrationDashboard> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var users = await GetUsersAsync(connection, cancellationToken);
        var activities = await GetActivitiesAsync(connection, cancellationToken);

        return new AdministrationDashboard(users, activities);
    }

    private static async Task<IReadOnlyList<AdministrationUser>> GetUsersAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.ID_utente,
                u.Utente,
                LTRIM(RTRIM(CONCAT(u.Nome, ' ', u.Cognome))) AS DisplayName,
                ISNULL(u.Permessi_CE, 0) AS PermissionLevel,
                ISNULL(CONVERT(INT, u.Bloccato), 0) AS IsBlocked,
                SUM(CASE WHEN p.InseritoDaUserId = u.ID_utente THEN 1 ELSE 0 END) AS InsertedTrips,
                SUM(CASE WHEN p.DeletedByUserId = u.ID_utente THEN 1 ELSE 0 END) AS DeletedTrips
            FROM dbo.Utenti AS u
            LEFT JOIN dbo.Percorrenze AS p
                ON p.InseritoDaUserId = u.ID_utente
                OR p.DeletedByUserId = u.ID_utente
            GROUP BY
                u.ID_utente,
                u.Utente,
                u.Nome,
                u.Cognome,
                u.Permessi_CE,
                u.Bloccato
            ORDER BY
                CASE WHEN ISNULL(CONVERT(INT, u.Bloccato), 0) = 0 THEN 0 ELSE 1 END,
                u.Cognome,
                u.Nome,
                u.Utente;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<AdministrationUser>();

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new AdministrationUser(
                reader.GetInt32(reader.GetOrdinal("ID_utente")),
                GetString(reader, "Utente", "—"),
                GetString(reader, "DisplayName", "Utente senza nome"),
                reader.GetInt32(reader.GetOrdinal("PermissionLevel")),
                reader.GetInt32(reader.GetOrdinal("IsBlocked")) != 0,
                reader.GetInt32(reader.GetOrdinal("InsertedTrips")),
                reader.GetInt32(reader.GetOrdinal("DeletedTrips"))));
        }

        return users;
    }

    private static async Task<IReadOnlyList<MileageAuditActivity>> GetActivitiesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (40)
                Id,
                DataPercorrenza,
                KmPartenza,
                KmArrivo,
                Tragitto,
                IsDeleted,
                InseritoDa,
                DeletedBy,
                DataCreazione,
                DeletedAt
            FROM dbo.vw_PercorrenzeConUtenti
            ORDER BY COALESCE(DeletedAt, DataCreazione) DESC, Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var activities = new List<MileageAuditActivity>();

        while (await reader.ReadAsync(cancellationToken))
        {
            activities.Add(new MileageAuditActivity(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetDateTime(reader.GetOrdinal("DataPercorrenza")),
                reader.GetInt32(reader.GetOrdinal("KmPartenza")),
                reader.GetInt32(reader.GetOrdinal("KmArrivo")),
                GetString(reader, "Tragitto", "—"),
                reader.GetBoolean(reader.GetOrdinal("IsDeleted")),
                GetString(reader, "InseritoDa", "Dato precedente"),
                GetNullableString(reader, "DeletedBy"),
                reader.GetDateTime(reader.GetOrdinal("DataCreazione")),
                GetNullableDateTime(reader, "DeletedAt")));
        }

        return activities;
    }

    private static string GetString(SqlDataReader reader, string columnName, string fallback) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? fallback
            : reader.GetString(reader.GetOrdinal(columnName));

    private static string? GetNullableString(SqlDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetString(reader.GetOrdinal(columnName));

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : reader.GetDateTime(reader.GetOrdinal(columnName));
}
