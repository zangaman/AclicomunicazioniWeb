namespace AcliComunicazioni.Application.Mileage;

public interface IMileageService
{
    Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MileageEntry>> GetAllAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<MileageEntry?> GetByIdAsync(
        int userId,
        int id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        int userId,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        int userId,
        int id,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default);
}
