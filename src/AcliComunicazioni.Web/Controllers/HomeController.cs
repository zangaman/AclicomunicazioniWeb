using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AcliComunicazioni.Application.Common.Interfaces;
using AcliComunicazioni.Application.Mileage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcliComunicazioni.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private readonly IMileageService _mileageService;
    private readonly ICurrentUser _currentUser;

    public HomeController(IMileageService mileageService, ICurrentUser currentUser)
    {
        _mileageService = mileageService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Challenge();

        var dashboard = await _mileageService.GetDashboardAsync(userId.Value, cancellationToken);
        return View(dashboard);
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
            return Challenge();

        var insertedBy = User.FindFirst("display_name")?.Value
            ?? User.Identity?.Name
            ?? $"Utente {userId.Value}";

        try
        {
            await _mileageService.AddAsync(
                userId.Value,
                insertedBy,
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
    public async Task<IActionResult> EditMileage(int id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Challenge();

        var entry = await _mileageService.GetByIdAsync(userId.Value, id, cancellationToken);
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
            return Challenge();

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
            return RedirectToAction(nameof(Index), null, "latest-trips");
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
                User.FindFirst("display_name")?.Value ?? User.Identity?.Name ?? "Utente",
                null,
                0);
            return View(entry);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportMileage(string format, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Challenge();

        var entries = await _mileageService.GetAllAsync(userId.Value, cancellationToken);
        var fileDate = DateTime.Today.ToString("yyyyMMdd");

        return format.ToLowerInvariant() switch
        {
            "json" => File(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true })),
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

    private static byte[] CreateCsv(IEnumerable<MileageEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Data;Tragitto;Km partenza;Km arrivo;Km percorsi;Km non registrati;Inserito da;Descrizione");

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

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static XDocument CreateXml(IEnumerable<MileageEntry> entries) =>
        new(
            new XElement("Percorrenze",
                entries.Select(entry =>
                    new XElement("Percorrenza",
                        new XAttribute("Id", entry.Id),
                        new XElement("Data", entry.TripDate.ToString("yyyy-MM-dd")),
                        new XElement("Tragitto", entry.Route),
                        new XElement("KmPartenza", entry.StartKilometers),
                        new XElement("KmArrivo", entry.EndKilometers),
                        new XElement("KmPercorsi", entry.DistanceKilometers),
                        new XElement("KmNonRegistrati", entry.GapKilometers),
                        new XElement("InseritoDa", entry.InsertedBy),
                        new XElement("Descrizione", entry.Description ?? string.Empty)))));
}