using AcliComunicazioni.Application.Mileage;

namespace AcliComunicazioni.Web.Models;

public sealed record MileageHistoryViewModel(
    MileageDashboard Dashboard,
    int? SelectedYear,
    int? SelectedMonth,
    IReadOnlyList<int> AvailableYears,
    bool IsAllHistory);
