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
        var entries = await GetEntriesAsync(50, cancellationToken);
        var latest = entries.FirstOrDefault();
        return new MileageDashboard(latest?.EndKilometers, latest?.TripDate, entries);
    }

    public Task<IReadOnlyList<MileageEntry>> GetAllAsync(int userId, CancellationToken cancellationToken = default) =>
        GetEntriesAsync(null, cancellationToken);

    public async Task<MileageEntry?> GetByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            WITH Ordinato AS
            (
                SELECT Id, IdUtente, KmPartenza, KmArrivo, KmPercorsi, DataPercorrenza,
                       Tragitto, Descrizione, DataCreazione, InseritoDa,
                       LAG(KmArrivo) OVER (ORDER BY DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
            )
            SELECT * FROM Ordinato
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
        string insertedBy,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var values = Validate(startKilometers, endKilometers, tripDate, route, description);
        var normalizedInsertedBy = string.IsNullOrWhiteSpace(insertedBy) ? $"Utente {userId}" : insertedBy.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string latestSql = """
            SELECT TOP (1) KmArrivo
            FROM dbo.Percorrenze
            ORDER BY DataPercorrenza DESC, Id DESC;
            """;

        await using (var latestCommand = new SqlCommand(latestSql, connection))
        {
            var latestValue = await latestCommand.ExecuteScalarAsync(cancellationToken);
            if (latestValue is not null && latestValue is not DBNull &&
                startKilometers < Convert.ToInt32(latestValue))
            {
                throw new InvalidOperationException(
                    "I chilometri di partenza non possono essere inferiori all'ultimo chilometraggio registrato del mezzo.");
            }
        }

        const string insertSql = """
            INSERT INTO dbo.Percorrenze
                (IdUtente, DataPercorrenza, KmPartenza, KmArrivo, Tragitto, Descrizione, InseritoDa, DataCreazione)
            VALUES
                (@UserId, @TripDate, @StartKilometers, @EndKilometers, @Route, @Description, @InsertedBy, SYSUTCDATETIME());
            """;

        await using var command = new SqlCommand(insertSql, connection);
        AddWriteParameters(command, userId, startKilometers, endKilometers, tripDate, values.Route, values.Description);
        command.Parameters.Add("@InsertedBy", SqlDbType.NVarChar, 150).Value = normalizedInsertedBy;
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

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("Percorrenza non trovata o non modificabile dall'utente corrente.");
    }

    private async Task<IReadOnlyList<MileageEntry>> GetEntriesAsync(int? top, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            WITH Ordinato AS
            (
                SELECT Id, KmPartenza, KmArrivo, KmPercorsi, DataPercorrenza,
                       Tragitto, Descrizione, DataCreazione, InseritoDa,
                       LAG(KmArrivo) OVER (ORDER BY DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
            )
            SELECT {(top.HasValue ? $"TOP ({top.Value})" : string.Empty)} *
            FROM Ordinato
            ORDER BY DataPercorrenza DESC, Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        var entries = new List<MileageEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            entries.Add(ReadEntry(reader));

        return entries;
    }

    private static MileageEntry ReadEntry(SqlDataReader reader)
    {
        var previousEndOrdinal = reader.GetOrdinal("KmArrivoPrecedente");
        int? previousEnd = reader.IsDBNull(previousEndOrdinal) ? null : reader.GetInt32(previousEndOrdinal);
        var start = reader.GetInt32(reader.GetOrdinal("KmPartenza"));

        return new MileageEntry(
            reader.GetInt32(reader.GetOrdinal("Id")),
            start,
            reader.GetInt32(reader.GetOrdinal("KmArrivo")),
            reader.GetInt32(reader.GetOrdinal("KmPercorsi")),
            reader.GetDateTime(reader.GetOrdinal("DataPercorrenza")),
            reader.GetString(reader.GetOrdinal("Tragitto")),
            reader.IsDBNull(reader.GetOrdinal("Descrizione")) ? null : reader.GetString(reader.GetOrdinal("Descrizione")),
            reader.GetDateTime(reader.GetOrdinal("DataCreazione")),
            reader.IsDBNull(reader.GetOrdinal("InseritoDa")) ? "Dato precedente" : reader.GetString(reader.GetOrdinal("InseritoDa")),
            previousEnd,
            previousEnd.HasValue && start > previousEnd.Value ? start - previousEnd.Value : 0);
    }

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
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = description is null ? DBNull.Value : description;
    }
}