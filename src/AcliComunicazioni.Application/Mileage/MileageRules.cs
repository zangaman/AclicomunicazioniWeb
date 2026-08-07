namespace AcliComunicazioni.Application.Mileage;

public sealed record MileageInterval(int StartKilometers, int EndKilometers);

public sealed record ValidatedMileageInput(string Route, string? Description);

public static class MileageRules
{
    public static ValidatedMileageInput ValidateInput(
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string? route,
        string? description,
        DateTime? today = null)
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

        if (tripDate.Date > (today ?? DateTime.Today).Date)
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

        return new ValidatedMileageInput(normalizedRoute, normalizedDescription);
    }

    public static void EnsureNoOverlap(
        int startKilometers,
        int endKilometers,
        IEnumerable<MileageInterval> existingIntervals)
    {
        var overlapping = existingIntervals.FirstOrDefault(existing =>
            existing.StartKilometers < endKilometers &&
            existing.EndKilometers > startKilometers);

        if (overlapping is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"La percorrenza {startKilometers:N0} → {endKilometers:N0} si sovrappone " +
            $"alla registrazione {overlapping.StartKilometers:N0} → {overlapping.EndKilometers:N0}. " +
            "Correggi i chilometri prima di salvare.");
    }

    public static void EnsureDateWithinNeighbors(
        DateTime tripDate,
        DateTime? previousTripDate,
        DateTime? nextTripDate)
    {
        var normalizedDate = tripDate.Date;
        var previousDate = previousTripDate?.Date;
        var nextDate = nextTripDate?.Date;

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

    public static int CalculateGap(int? previousEndKilometers, int startKilometers) =>
        previousEndKilometers.HasValue && startKilometers > previousEndKilometers.Value
            ? startKilometers - previousEndKilometers.Value
            : 0;
}
