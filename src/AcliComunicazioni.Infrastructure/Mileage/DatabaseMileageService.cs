using System.Data;
using AcliComunicazioni.Application.Mileage;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AcliComunicazioni.Infrastructure.Mileage;

public sealed class DatabaseMileageService : IMileageService
{
    private readonly string _connectionString;

    public DatabaseMileageService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' non configurata.");
    }

    public async Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (10)
                Id,
                Chilometri,
                DataRilevazione,
                DataCreazione
            FROM dbo.RilevazioniChilometriche
            WHERE IdUtente = @UserId
            ORDER BY DataRilevazione DESC, Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        var entries = new List<MileageEntry>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new MileageEntry(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetInt32(reader.GetOrdinal("Chilometri")),
                reader.GetDateTime(reader.GetOrdinal("DataRilevazione")),
                reader.GetDateTime(reader.GetOrdinal("DataCreazione"))));
        }

        var latest = entries.FirstOrDefault();
        return new MileageDashboard(
            latest?.Kilometers,
            latest?.ReadingDate,
            entries);
    }

    public async Task AddAsync(
        int userId,
        int kilometers,
        DateTime readingDate,
        CancellationToken cancellationToken = default)
    {
        if (kilometers <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kilometers),
                "I chilometri devono essere maggiori di zero.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string latestSql = """
            SELECT TOP (1) Chilometri
            FROM dbo.RilevazioniChilometriche
            WHERE IdUtente = @UserId
            ORDER BY DataRilevazione DESC, Id DESC;
            """;

        await using (var latestCommand = new SqlCommand(latestSql, connection))
        {
            latestCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            var latestValue = await latestCommand.ExecuteScalarAsync(cancellationToken);

            if (latestValue is not null &&
                latestValue is not DBNull &&
                kilometers < Convert.ToInt32(latestValue))
            {
                throw new InvalidOperationException(
                    "Il nuovo valore non può essere inferiore all'ultima rilevazione.");
            }
        }

        const string insertSql = """
            INSERT INTO dbo.RilevazioniChilometriche
                (IdUtente, Chilometri, DataRilevazione, DataCreazione)
            VALUES
                (@UserId, @Kilometers, @ReadingDate, SYSUTCDATETIME());
            """;

        await using var command = new SqlCommand(insertSql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@Kilometers", SqlDbType.Int).Value = kilometers;
        command.Parameters.Add("@ReadingDate", SqlDbType.DateTime2).Value = readingDate;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
