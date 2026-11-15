using Microsoft.AspNetCore.Authorization;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AccessGuard.Models;

namespace AccessGuard.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        ViewData["Page"] = "dashboard";
        ViewData["Title"] = "Vue d’ensemble";
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        ViewData["Page"] = "status";
        ViewData["Title"] = "Données de démonstration";
        return View();
    }

    [AllowAnonymous]
    public IActionResult Status(int code = 404)
    {
        if (code < 400 || code > 599) code = 404;
        ViewData["Page"] = "status";
        ViewData["Title"] = code == 404 ? "Page introuvable" : "Page indisponible";
        ViewData["Code"] = code;
        Response.StatusCode = code;
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [AllowAnonymous]
    public IActionResult Error()
    {
        ViewData["Page"] = "status";
        ViewData["Title"] = "Une erreur est survenue";
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
