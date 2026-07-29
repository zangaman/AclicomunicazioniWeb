namespace AcliComunicazioni.Application.Mileage;

public sealed record MileageDashboard(
    int? CurrentKilometers,
    DateTime? LastReadingDate,
    IReadOnlyList<MileageEntry> RecentEntries);

public sealed record MileageEntry(
    int Id,
    int Kilometers,
    DateTime ReadingDate,
    DateTime CreatedAt);
