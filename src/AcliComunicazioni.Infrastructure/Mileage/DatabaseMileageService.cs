using System.Data;
using System.Data.Common;
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

    public async Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetEntriesAsync(50, cancellationToken);
        var latest = entries.FirstOrDefault();

        return new MileageDashboard(
            latest?.EndKilometers,
            latest?.TripDate,
            entries);
    }

    public Task<IReadOnlyList<MileageEntry>> GetAllAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        GetEntriesAsync(null, cancellationToken);

    public async Task<MileageEntry?> GetByIdAsync(
        int userId,
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            WITH Ordinato AS
            (
                SELECT
                    Id,
                    IdUtente,
                    KmPartenza,
                    KmArrivo,
                    KmPercorsi,
                    DataPercorrenza,
                    Tragitto,
                    Descrizione,
                    DataCreazione,
                    InseritoDa,
                    LAG(KmArrivo) OVER (ORDER BY DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
            )
            SELECT
                Id,
                KmPartenza,
                KmArrivo,
                KmPercorsi,
                DataPercorrenza,
                Tragitto,
                Descrizione,
                DataCreazione,
                InseritoDa,
                KmArrivoPrecedente
            FROM Ordinato
            WHERE IdUtente = @UserId
              AND Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadEntry(reader)
            : null;
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
        var values = Validate(
            startKilometers,
            endKilometers,
            tripDate,
            route,
            description);

        var normalizedInsertedBy = string.IsNullOrWhiteSpace(insertedBy)
            ? $"Utente {userId}"
            : insertedBy.Trim();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO dbo.Percorrenze
                (IdUtente, DataPercorrenza, KmPartenza, KmArrivo, Tragitto, Descrizione, InseritoDa, DataCreazione)
            VALUES
                (@UserId, @TripDate, @StartKilometers, @EndKilometers, @Route, @Description, @InsertedBy, SYSUTCDATETIME());
            """;

        await using var command = new SqlCommand(sql, connection);
        AddWriteParameters(
            command,
            userId,
            startKilometers,
            endKilometers,
            tripDate,
            values.Route,
            values.Description);

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
        var values = Validate(
            startKilometers,
            endKilometers,
            tripDate,
            route,
            description);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Percorrenze
            SET DataPercorrenza = @TripDate,
                KmPartenza = @StartKilometers,
                KmArrivo = @EndKilometers,
                Tragitto = @Route,
                Descrizione = @Description
            WHERE Id = @Id
              AND IdUtente = @UserId;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        AddWriteParameters(
            command,
            userId,
            startKilometers,
            endKilometers,
            tripDate,
            values.Route,
            values.Description);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            throw new InvalidOperationException(
                "Percorrenza non trovata o non modificabile dall'utente corrente.");
        }
    }

    private async Task<IReadOnlyList<MileageEntry>> GetEntriesAsync(
        int? top,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var topClause = top.HasValue ? $"TOP ({top.Value})" : string.Empty;

        var sql = $"""
            WITH Ordinato AS
            (
                SELECT
                    Id,
                    KmPartenza,
                    KmArrivo,
                    KmPercorsi,
                    DataPercorrenza,
                    Tragitto,
                    Descrizione,
                    DataCreazione,
                    InseritoDa,
                    LAG(KmArrivo) OVER (ORDER BY DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
            )
            SELECT {topClause}
                Id,
                KmPartenza,
                KmArrivo,
                KmPercorsi,
                DataPercorrenza,
                Tragitto,
                Descrizione,
                DataCreazione,
                InseritoDa,
                KmArrivoPrecedente
            FROM Ordinato
            ORDER BY DataPercorrenza DESC, Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        var entries = new List<MileageEntry>();

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private static MileageEntry ReadEntry(DbDataReader reader)
    {
        var startKilometers = reader.GetInt32(reader.GetOrdinal("KmPartenza"));
        var previousEndKilometers = GetNullableInt32(reader, "KmArrivoPrecedente");

        var gapKilometers = previousEndKilometers.HasValue &&
                            startKilometers > previousEndKilometers.Value
            ? startKilometers - previousEndKilometers.Value
            : 0;

        return new MileageEntry(
            reader.GetInt32(reader.GetOrdinal("Id")),
            startKilometers,
            reader.GetInt32(reader.GetOrdinal("KmArrivo")),
            reader.GetInt32(reader.GetOrdinal("KmPercorsi")),
            reader.GetDateTime(reader.GetOrdinal("DataPercorrenza")),
            reader.GetString(reader.GetOrdinal("Tragitto")),
            GetNullableString(reader, "Descrizione"),
            reader.GetDateTime(reader.GetOrdinal("DataCreazione")),
            GetNullableString(reader, "InseritoDa") ?? "Dato precedente",
            previousEndKilometers,
            gapKilometers);
    }

    private static int? GetNullableInt32(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? GetNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static (string Route, string? Description) Validate(
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description)
    {
        if (startKilometers < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startKilometers),
                "I chilometri iniziali non possono essere negativi.");
        }

        if (endKilometers < startKilometers)
        {
            throw new InvalidOperationException(
                "I chilometri finali non possono essere inferiori a quelli iniziali.");
        }

        if (tripDate.Date > DateTime.Today)
        {
            throw new InvalidOperationException(
                "La data della percorrenza non può essere futura.");
        }

        var normalizedRoute = route?.Trim() ?? string.Empty;

        if (normalizedRoute.Length == 0)
        {
            throw new InvalidOperationException("Inserisci il tragitto.");
        }

        if (normalizedRoute.Length > 200)
        {
            throw new InvalidOperationException(
                "Il tragitto non può superare 200 caratteri.");
        }

        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription?.Length > 500)
        {
            throw new InvalidOperationException(
                "Il dettaglio non può superare 500 caratteri.");
        }

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
