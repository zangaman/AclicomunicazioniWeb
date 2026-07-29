namespace AcliComunicazioni.Application.Mileage;

public interface IMileageService
{
    Task<MileageDashboard> GetDashboardAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        int userId,
        int kilometers,
        DateTime readingDate,
        CancellationToken cancellationToken = default);
}
