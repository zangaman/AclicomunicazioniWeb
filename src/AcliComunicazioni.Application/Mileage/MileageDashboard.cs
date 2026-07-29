namespace AcliComunicazioni.Application.Mileage;

public sealed record MileageDashboard(
    int? CurrentKilometers,
    DateTime? LastTripDate,
    IReadOnlyList<MileageEntry> RecentEntries);

public sealed record MileageEntry(
    int Id,
    int StartKilometers,
    int EndKilometers,
    int DistanceKilometers,
    DateTime TripDate,
    string Route,
    string? Description,
    DateTime CreatedAt);
