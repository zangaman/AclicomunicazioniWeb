namespace AcliComunicazioni.Application.Mileage;

public interface IMileageService
{
    Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        int userId,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken = default);
}
