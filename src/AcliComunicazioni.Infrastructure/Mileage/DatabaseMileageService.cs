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

    public async Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var entries = await GetEntriesAsync(null, cancellationToken);
        var latest = entries.FirstOrDefault();
        var current = entries.MaxBy(entry => entry.EndKilometers);

        return new MileageDashboard(
            current?.EndKilometers,
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
                    InseritoDaUserId,
                    LAG(KmArrivo) OVER (ORDER BY KmPartenza, KmArrivo, DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
                WHERE IsDeleted = 0
            )
            SELECT
                p.Id,
                p.KmPartenza,
                p.KmArrivo,
                p.KmPercorsi,
                p.DataPercorrenza,
                p.Tragitto,
                p.Descrizione,
                p.DataCreazione,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CONCAT(u.Nome, ' ', u.Cognome))), ''),
                    NULLIF(LTRIM(RTRIM(p.InseritoDa)), ''),
                    CONCAT('Utente ', COALESCE(CONVERT(NVARCHAR(12), p.InseritoDaUserId), CONVERT(NVARCHAR(12), p.IdUtente)))
                ) AS InseritoDa,
                p.KmArrivoPrecedente
            FROM Ordinato AS p
            LEFT JOIN dbo.Utenti AS u ON u.ID_utente = p.InseritoDaUserId
            WHERE p.IdUtente = @UserId
              AND p.Id = @Id;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? ReadEntry(reader)
            : null;
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
        var values = Validate(
            startKilometers,
            endKilometers,
            tripDate,
            route,
            description);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await EnsureNoOverlapAsync(
            connection,
            startKilometers,
            endKilometers,
            excludeId: null,
            cancellationToken);

        await EnsureDateWithinNeighboringTripsAsync(
            connection,
            startKilometers,
            endKilometers,
            tripDate,
            excludeId: null,
            cancellationToken);

        const string sql = """
            INSERT INTO dbo.Percorrenze
                (IdUtente, InseritoDaUserId, DataPercorrenza, KmPartenza, KmArrivo, Tragitto, Descrizione, DataCreazione)
            VALUES
                (@UserId, @InseritoDaUserId, @TripDate, @StartKilometers, @EndKilometers, @Route, @Description, SYSUTCDATETIME());
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

        command.Parameters.Add("@InseritoDaUserId", SqlDbType.Int).Value = userId;

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

        await EnsureNoOverlapAsync(
            connection,
            startKilometers,
            endKilometers,
            excludeId: id,
            cancellationToken);

        await EnsureDateWithinNeighboringTripsAsync(
            connection,
            startKilometers,
            endKilometers,
            tripDate,
            excludeId: id,
            cancellationToken);

        const string sql = """
            UPDATE dbo.Percorrenze
            SET DataPercorrenza = @TripDate,
                KmPartenza = @StartKilometers,
                KmArrivo = @EndKilometers,
                Tragitto = @Route,
                Descrizione = @Description
            WHERE Id = @Id
              AND IdUtente = @UserId
              AND IsDeleted = 0;
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

    public async Task DeleteAsync(
        int userId,
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE dbo.Percorrenze
            SET IsDeleted = 1,
                DeletedAt = SYSUTCDATETIME(),
                DeletedByUserId = @DeletedByUserId
            WHERE Id = @Id
              AND IdUtente = @UserId
              AND IsDeleted = 0;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@DeletedByUserId", SqlDbType.Int).Value = userId;

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows == 0)
        {
            throw new InvalidOperationException(
                "Percorrenza non trovata o già eliminata.");
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
                    IdUtente,
                    KmPartenza,
                    KmArrivo,
                    KmPercorsi,
                    DataPercorrenza,
                    Tragitto,
                    Descrizione,
                    DataCreazione,
                    InseritoDa,
                    InseritoDaUserId,
                    LAG(KmArrivo) OVER (ORDER BY KmPartenza, KmArrivo, DataPercorrenza, Id) AS KmArrivoPrecedente
                FROM dbo.Percorrenze
                WHERE IsDeleted = 0
            )
            SELECT {topClause}
                p.Id,
                p.KmPartenza,
                p.KmArrivo,
                p.KmPercorsi,
                p.DataPercorrenza,
                p.Tragitto,
                p.Descrizione,
                p.DataCreazione,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CONCAT(u.Nome, ' ', u.Cognome))), ''),
                    NULLIF(LTRIM(RTRIM(p.InseritoDa)), ''),
                    CONCAT('Utente ', COALESCE(CONVERT(NVARCHAR(12), p.InseritoDaUserId), CONVERT(NVARCHAR(12), p.IdUtente)))
                ) AS InseritoDa,
                p.KmArrivoPrecedente
            FROM Ordinato AS p
            LEFT JOIN dbo.Utenti AS u ON u.ID_utente = p.InseritoDaUserId
            ORDER BY p.DataPercorrenza DESC, p.Id DESC;
            """;

        await using var command = new SqlCommand(sql, connection);
        var entries = new List<MileageEntry>();

        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    private static MileageEntry ReadEntry(SqlDataReader reader)
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

    private static int? GetNullableInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? GetNullableString(SqlDataReader reader, string columnName)
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

    private static async Task EnsureDateWithinNeighboringTripsAsync(
        SqlConnection connection,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT TOP (1) DataPercorrenza
                 FROM dbo.Percorrenze
                 WHERE IsDeleted = 0
                   AND KmArrivo = @StartKilometers
                   AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
                 ORDER BY DataPercorrenza DESC, Id DESC) AS PreviousTripDate,
                (SELECT TOP (1) DataPercorrenza
                 FROM dbo.Percorrenze
                 WHERE IsDeleted = 0
                   AND KmPartenza = @EndKilometers
                   AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
                 ORDER BY DataPercorrenza ASC, Id ASC) AS NextTripDate;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@StartKilometers", SqlDbType.Int).Value = startKilometers;
        command.Parameters.Add("@EndKilometers", SqlDbType.Int).Value = endKilometers;
        command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value =
            excludeId.HasValue ? excludeId.Value : DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return;
        }

        var previousDate = reader.IsDBNull(reader.GetOrdinal("PreviousTripDate"))
            ? (DateTime?)null
            : reader.GetDateTime(reader.GetOrdinal("PreviousTripDate")).Date;
        var nextDate = reader.IsDBNull(reader.GetOrdinal("NextTripDate"))
            ? (DateTime?)null
            : reader.GetDateTime(reader.GetOrdinal("NextTripDate")).Date;
        var normalizedDate = tripDate.Date;

        if (previousDate.HasValue && normalizedDate < previousDate.Value)
        {
            throw new InvalidOperationException(
                $"La data deve essere uguale o successiva al {previousDate:dd/MM/yyyy}.");
        }

        if (nextDate.HasValue && normalizedDate > nextDate.Value)
        {
            throw new InvalidOperationException(
                $"La data deve essere uguale o precedente al {nextDate:dd/MM/yyyy}.");
        }
    }

    private static async Task EnsureNoOverlapAsync(
        SqlConnection connection,
        int startKilometers,
        int endKilometers,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Id,
                KmPartenza,
                KmArrivo
            FROM dbo.Percorrenze
            WHERE IsDeleted = 0
              AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
              AND KmPartenza < @EndKilometers
              AND KmArrivo > @StartKilometers;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@StartKilometers", SqlDbType.Int).Value = startKilometers;
        command.Parameters.Add("@EndKilometers", SqlDbType.Int).Value = endKilometers;
        command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value =
            excludeId.HasValue ? excludeId.Value : DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            var existingStart = reader.GetInt32(reader.GetOrdinal("KmPartenza"));
            var existingEnd = reader.GetInt32(reader.GetOrdinal("KmArrivo"));

            throw new InvalidOperationException(
                $"La percorrenza {startKilometers:N0} → {endKilometers:N0} si sovrappone " +
                $"alla registrazione {existingStart:N0} → {existingEnd:N0}. " +
                "Correggi i chilometri prima di salvare.");
        }
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
