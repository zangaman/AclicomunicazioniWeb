using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AcliComunicazioni.Application.Common.Interfaces;
using AcliComunicazioni.Application.Mileage;
using AcliComunicazioni.Web.Models;
using AcliComunicazioni.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcliComunicazioni.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly IMileageService _mileageService;
    private readonly ICurrentUser _currentUser;
    private readonly IWebHostEnvironment _environment;

    public HomeController(
        IMileageService mileageService,
        ICurrentUser currentUser,
        IWebHostEnvironment environment)
    {
        _mileageService = mileageService;
        _currentUser = currentUser;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? year,
        int? month,
        bool? showAll,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        var dashboard = await _mileageService.GetDashboardAsync(
            userId.Value,
            cancellationToken);

        return View(CreateHistoryViewModel(dashboard, year, month, showAll == true));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMileage(
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await _mileageService.AddAsync(
                userId.Value,
                startKilometers,
                endKilometers,
                tripDate == default ? DateTime.Today : tripDate,
                route,
                description,
                cancellationToken);

            TempData["MileageSuccess"] = "Percorrenza registrata correttamente.";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            TempData["MileageError"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> EditMileage(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        var entry = await _mileageService.GetByIdAsync(
            userId.Value,
            id,
            cancellationToken);

        return entry is null ? NotFound() : View(entry);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMileage(
        int id,
        int startKilometers,
        int endKilometers,
        DateTime tripDate,
        string route,
        string? description,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await _mileageService.UpdateAsync(
                userId.Value,
                id,
                startKilometers,
                endKilometers,
                tripDate,
                route,
                description,
                cancellationToken);

            TempData["MileageSuccess"] = "Percorrenza modificata correttamente.";

            return Redirect($"{Url.Action(nameof(Index))}#latest-trips");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            ModelState.AddModelError(string.Empty, exception.Message);

            var entry = new MileageEntry(
                id,
                startKilometers,
                endKilometers,
                Math.Max(0, endKilometers - startKilometers),
                tripDate,
                route,
                description,
                DateTime.UtcNow,
                CurrentDisplayName(userId.Value),
                null,
                0);

            return View(entry);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMileage(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        try
        {
            await _mileageService.DeleteAsync(
                userId.Value,
                id,
                cancellationToken);

            TempData["MileageSuccess"] = "Percorrenza eliminata correttamente. Lo storico è stato conservato.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["MileageError"] = exception.Message;
        }

        return Redirect($"{Url.Action(nameof(Index))}#latest-trips");
    }

    [HttpGet]
    public async Task<IActionResult> ExportMileage(
        string? format,
        int? year,
        int? month,
        bool? showAll,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        var dashboard = await _mileageService.GetDashboardAsync(
            userId.Value,
            cancellationToken);
        var history = CreateHistoryViewModel(dashboard, year, month, showAll == true);
        var entries = history.Dashboard.RecentEntries;

        var fileDate = DateTime.Today.ToString("yyyyMMdd");
        var normalizedFormat = format?.Trim().ToLowerInvariant() ?? "csv";

        return normalizedFormat switch
        {
            "json" => File(
                Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(
                        entries,
                        new JsonSerializerOptions { WriteIndented = true })),
                "application/json",
                $"percorrenze-{fileDate}.json"),

            "xml" => File(
                Encoding.UTF8.GetBytes(CreateXml(entries).ToString()),
                "application/xml",
                $"percorrenze-{fileDate}.xml"),

            _ => File(
                CreateCsv(entries),
                "text/csv; charset=utf-8",
                $"percorrenze-{fileDate}.csv")
        };
    }

    [HttpGet]
    public async Task<IActionResult> MileageReport(
        int? year,
        int? month,
        bool? showAll,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        var dashboard = await _mileageService.GetDashboardAsync(
            userId.Value,
            cancellationToken);

        return View(CreateHistoryViewModel(dashboard, year, month, showAll == true));
    }

    [HttpGet]
    public async Task<IActionResult> DownloadMileagePdf(
        int? year,
        int? month,
        bool? showAll,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Challenge();
        }

        var dashboard = await _mileageService.GetDashboardAsync(
            userId.Value,
            cancellationToken);
        var history = CreateHistoryViewModel(dashboard, year, month, showAll == true);
        var period = history.IsAllHistory
            ? "tutto-storico"
            : history.SelectedMonth.HasValue
                ? $"{history.SelectedYear!.Value}-{history.SelectedMonth.Value:00}"
                : history.SelectedYear?.ToString() ?? DateTime.Today.ToString("yyyy-MM");

        return File(
            MileagePdfGenerator.Create(
                history,
                Path.Combine(_environment.WebRootPath, "images", "logo-acli.png")),
            "application/pdf",
            $"percorrenze-{period}.pdf");
    }

    private static MileageHistoryViewModel CreateHistoryViewModel(
        MileageDashboard dashboard,
        int? year,
        int? month,
        bool showAll)
    {
        var availableYears = dashboard.RecentEntries
            .Select(entry => entry.TripDate.Year)
            .Append(DateTime.Today.Year)
            .Distinct()
            .OrderByDescending(value => value)
            .ToArray();

        var selectedYear = showAll
            ? null
            : year is >= 2000 and <= 2100
                ? year
                : DateTime.Today.Year;
        var selectedMonth = showAll || month == 0
            ? null
            : month is >= 1 and <= 12
                ? month
                : DateTime.Today.Month;

        var entries = dashboard.RecentEntries
            .Where(entry => !selectedYear.HasValue || entry.TripDate.Year == selectedYear.Value)
            .Where(entry => !selectedMonth.HasValue || entry.TripDate.Month == selectedMonth.Value)
            .ToArray();

        return new MileageHistoryViewModel(
            dashboard with { RecentEntries = entries },
            dashboard.RecentEntries,
            selectedYear,
            selectedMonth,
            availableYears,
            showAll);
    }

    private string CurrentDisplayName(int userId) =>
        User.FindFirst("display_name")?.Value
        ?? User.Identity?.Name
        ?? $"Utente {userId}";

    private static byte[] CreateCsv(IEnumerable<MileageEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "Data;Tragitto;Km iniziali;Km finali;Km percorsi;Km non registrati;Conducente;Dettaglio");

        foreach (var entry in entries)
        {
            builder.Append(entry.TripDate.ToString("dd/MM/yyyy")).Append(';')
                .Append(Csv(entry.Route)).Append(';')
                .Append(entry.StartKilometers).Append(';')
                .Append(entry.EndKilometers).Append(';')
                .Append(entry.DistanceKilometers).Append(';')
                .Append(entry.GapKilometers).Append(';')
                .Append(Csv(entry.InsertedBy)).Append(';')
                .Append(Csv(entry.Description ?? string.Empty)).AppendLine();
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            .GetBytes(builder.ToString());
    }

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static XDocument CreateXml(IEnumerable<MileageEntry> entries) =>
        new(
            new XElement(
                "Percorrenze",
                entries.Select(entry =>
                    new XElement(
                        "Percorrenza",
                        new XAttribute("Id", entry.Id),
                        new XElement("Data", entry.TripDate.ToString("yyyy-MM-dd")),
                        new XElement("Tragitto", entry.Route),
                        new XElement("KmIniziali", entry.StartKilometers),
                        new XElement("KmFinali", entry.EndKilometers),
                        new XElement("KmPercorsi", entry.DistanceKilometers),
                        new XElement("KmNonRegistrati", entry.GapKilometers),
                        new XElement("Conducente", entry.InsertedBy),
                        new XElement("Dettaglio", entry.Description ?? string.Empty)))));
}
