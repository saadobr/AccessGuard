using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AccessGuard.Controllers;

public class PortalController : Controller
{
    private IActionResult Page(string page, string title, string view = "Index") {
        ViewData["Page"] = page;
        ViewData["Title"] = title;
        return View(view);
    }
    public IActionResult Index() => RedirectToAction(nameof(Audit));
    [Authorize(Policy="users.read")]
    public IActionResult Users() => Page("users", "Utilisateurs");
    [Authorize(Policy="roles.write")]
    public IActionResult Roles() => Page("roles", "Rôles et permissions");
    [Authorize(Policy="audit.read")]
    public IActionResult Audit() => Page("audit", "Audit & SOC", "Audit/Index");
    public IActionResult Account() => Page("account", "Mon compte");
    [AllowAnonymous]
    public IActionResult Login() => Page("login", "Connexion", "Login");
    [AllowAnonymous]
    public IActionResult Initialize() => Page("initialize", "Créer le premier administrateur", "Initialize");
    public IActionResult Setup2FA() => Page("setup", "Configuration 2FA", "Setup2FA");
}
