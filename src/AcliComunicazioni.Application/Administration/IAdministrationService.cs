namespace AcliComunicazioni.Application.Administration;

public interface IAdministrationService
{
    Task<AdministrationDashboard> GetDashboardAsync(
        CancellationToken cancellationToken = default);
}

public sealed record AdministrationDashboard(
    IReadOnlyList<AdministrationUser> Users,
    IReadOnlyList<MileageAuditActivity> RecentActivities);

public sealed record AdministrationUser(
    int Id,
    string Username,
    string DisplayName,
    int PermissionLevel,
    bool IsBlocked,
    int InsertedTrips,
    int DeletedTrips);

public sealed record MileageAuditActivity(
    int Id,
    DateTime TripDate,
    int StartKilometers,
    int EndKilometers,
    string Route,
    bool IsDeleted,
    string InsertedBy,
    string? DeletedBy,
    DateTime CreatedAt,
    DateTime? DeletedAt);
