using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AcliComunicazioni.Web.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
