using AcliComunicazioni.Application.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcliComunicazioni.Web.Controllers;

[Authorize]
public sealed class AdministrationController(
    IAdministrationService administrationService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var dashboard = await administrationService.GetDashboardAsync(cancellationToken);
        return View(dashboard);
    }
}
