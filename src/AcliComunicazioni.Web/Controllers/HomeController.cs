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

    public HomeController(
        IMileageService mileageService,
        ICurrentUser currentUser)
    {
        _mileageService = mileageService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
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

        return View(dashboard);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMileage(
        int kilometers,
        DateTime readingDate,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Challenge();
        }

        if (kilometers <= 0)
        {
            TempData["MileageError"] =
                "Inserisci un valore di chilometri valido.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _mileageService.AddAsync(
                userId.Value,
                kilometers,
                readingDate == default ? DateTime.Today : readingDate,
                cancellationToken);

            TempData["MileageSuccess"] =
                "Chilometri registrati correttamente.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["MileageError"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
