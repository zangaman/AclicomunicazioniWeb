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
        var entries = await GetEntriesAsync(userId, 50, cancellationToken);
        var latest = entries.FirstOrDefault();
        return new MileageDashboard(latest?.EndKilometers, latest?.TripDate, entries);
    }

    public Task<IReadOnlyList<MileageEntry>> GetAllAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        GetEntriesAsync(userId, null, cancellationToken);

    public async Task<MileageEntry?> GetByIdAsync(
        int userId,
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT Id, KmPartenza, KmArrivo, KmPercorsi, DataPercorrenza,
                   Tragitto, Descrizione, DataCreazione
            FROM dbo.Percorrenze
            WHERE IdUtente = @UserId AND Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEntry(reader) : null;
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
        var values = Validate(startKilometers, endKilometers, tripDate, route, description);

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
        AddWriteParameters(command, userId, startKilometers, endKilometers, tripDate, values.Route, values.Description);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        int userId,
        int id,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var values = Validate(startKilometers, endKilometers, tripDate, route, description);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Percorrenze
            SET DataPercorrenza = @TripDate,
                KmPartenza = @StartKilometers,
                KmArrivo = @EndKilometers,
                Tragitto = @Route,
                Descrizione = @Description
            WHERE Id = @Id AND IdUtente = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        AddWriteParameters(command, userId, startKilometers, endKilometers, tripDate, values.Route, values.Description);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
            throw new InvalidOperationException("Percorrenza non trovata.");
    }

    private async Task<IReadOnlyList<MileageEntry>> GetEntriesAsync(
        int userId,
        int? top,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT {(top.HasValue ? $"TOP ({top.Value})" : string.Empty)}
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
            entries.Add(ReadEntry(reader));

        return entries;
    }

    private static MileageEntry ReadEntry(SqlDataReader reader) => new(
        reader.GetInt32(reader.GetOrdinal("Id")),
        reader.GetInt32(reader.GetOrdinal("KmPartenza")),
        reader.GetInt32(reader.GetOrdinal("KmArrivo")),
        reader.GetInt32(reader.GetOrdinal("KmPercorsi")),
        reader.GetDateTime(reader.GetOrdinal("DataPercorrenza")),
        reader.GetString(reader.GetOrdinal("Tragitto")),
        reader.IsDBNull(reader.GetOrdinal("Descrizione"))
            ? null
            : reader.GetString(reader.GetOrdinal("Descrizione")),
        reader.GetDateTime(reader.GetOrdinal("DataCreazione")));

    private static (string Route, string? Description) Validate(
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description)
    {
        if (startKilometers < 0)
            throw new ArgumentOutOfRangeException(nameof(startKilometers), "I chilometri di partenza non possono essere negativi.");
        if (endKilometers < startKilometers)
            throw new InvalidOperationException("I chilometri di arrivo non possono essere inferiori a quelli di partenza.");
        if (tripDate.Date > DateTime.Today)
            throw new InvalidOperationException("La data della percorrenza non può essere futura.");

        var normalizedRoute = route?.Trim() ?? string.Empty;
        if (normalizedRoute.Length == 0)
            throw new InvalidOperationException("Inserisci il tragitto.");
        if (normalizedRoute.Length > 200)
            throw new InvalidOperationException("Il tragitto non può superare 200 caratteri.");

        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > 500)
            throw new InvalidOperationException("La descrizione non può superare 500 caratteri.");

        return (normalizedRoute, normalizedDescription);
    }

    private static void AddWriteParameters(
        SqlCommand command,
        int userId,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description)
    {
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@TripDate", SqlDbType.Date).Value = tripDate.Date;
        command.Parameters.Add("@StartKilometers", SqlDbType.Int).Value = startKilometers;
        command.Parameters.Add("@EndKilometers", SqlDbType.Int).Value = endKilometers;
        command.Parameters.Add("@Route", SqlDbType.NVarChar, 200).Value = route;
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value =
            description is null ? DBNull.Value : description;
    }
}
