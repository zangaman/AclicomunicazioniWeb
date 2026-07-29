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
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' non configurata.");
    }

    public async Task<MileageDashboard> GetDashboardAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT TOP (10)
                Id, KmPartenza, KmArrivo, KmPercorsi, DataPercorrenza,
                Tragitto, Descrizione, DataCreazione
            FROM dbo.Percorrenze
            WHERE IdUtente = @UserId
            ORDER BY DataPercorrenza DESC, Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        var entries = new List<MileageEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new MileageEntry(
                reader.GetInt32(reader.GetOrdinal("Id")),
                reader.GetInt32(reader.GetOrdinal("KmPartenza")),
                reader.GetInt32(reader.GetOrdinal("KmArrivo")),
                reader.GetInt32(reader.GetOrdinal("KmPercorsi")),
                reader.GetDateTime(reader.GetOrdinal("DataPercorrenza")),
                reader.GetString(reader.GetOrdinal("Tragitto")),
                reader.IsDBNull(reader.GetOrdinal("Descrizione"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Descrizione")),
                reader.GetDateTime(reader.GetOrdinal("DataCreazione"))));
        }

        var latest = entries.FirstOrDefault();
        return new MileageDashboard(latest?.EndKilometers, latest?.TripDate, entries);
    }

    public async Task AddAsync(
        int userId,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (startKilometers < 0)
            throw new ArgumentOutOfRangeException(nameof(startKilometers), "I chilometri di partenza non possono essere negativi.");

        if (endKilometers < startKilometers)
            throw new InvalidOperationException("I chilometri di arrivo non possono essere inferiori a quelli di partenza.");

        if (string.IsNullOrWhiteSpace(route))
            throw new InvalidOperationException("Inserisci il tragitto.");

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string latestSql = """
            SELECT TOP (1) KmArrivo
            FROM dbo.Percorrenze
            WHERE IdUtente = @UserId
            ORDER BY DataPercorrenza DESC, Id DESC;
            """;

        await using (var latestCommand = new SqlCommand(latestSql, connection))
        {
            latestCommand.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            var latestValue = await latestCommand.ExecuteScalarAsync(cancellationToken);

            if (latestValue is not null && latestValue is not DBNull &&
                startKilometers < Convert.ToInt32(latestValue))
            {
                throw new InvalidOperationException(
                    "I chilometri di partenza non possono essere inferiori all'ultimo chilometraggio registrato.");
            }
        }

        const string insertSql = """
            INSERT INTO dbo.Percorrenze
                (IdUtente, DataPercorrenza, KmPartenza, KmArrivo, Tragitto, Descrizione, DataCreazione)
            VALUES
                (@UserId, @TripDate, @StartKilometers, @EndKilometers, @Route, @Description, SYSUTCDATETIME());
            """;

        await using var command = new SqlCommand(insertSql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@TripDate", SqlDbType.Date).Value = tripDate.Date;
        command.Parameters.Add("@StartKilometers", SqlDbType.Int).Value = startKilometers;
        command.Parameters.Add("@EndKilometers", SqlDbType.Int).Value = endKilometers;
        command.Parameters.Add("@Route", SqlDbType.NVarChar, 200).Value = route.Trim();
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
            string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim();

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
