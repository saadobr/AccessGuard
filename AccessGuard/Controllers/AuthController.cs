using AccessGuard.Data;
using AccessGuard.Models;
using AccessGuard.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace AccessGuard.Controllers;
[ApiController,Route("api/auth")]
public class AuthController(AppDbContext db,UserManager<AppUser> users,SignInManager<AppUser> signin,AuditService audit,IAntiforgery csrf,IWebHostEnvironment env) : ControllerBase {
    [AllowAnonymous,HttpGet("session")]
    public async Task<IActionResult> Session(){var me=await users.GetUserAsync(User);return Ok(new{authenticated=me!=null,bootstrap=await db.Users.CountAsync()==0,token=csrf.GetAndStoreTokens(HttpContext).RequestToken,name=me?.FullName});}
    [AllowAnonymous,HttpPost("bootstrap"),EnableRateLimiting("authentication")]
    public async Task<IActionResult> Bootstrap(BootstrapRequest r){
        var ip=HttpContext.Connection.RemoteIpAddress;
        if(!env.IsDevelopment()||ip==null||!IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6?ip.MapToIPv4():ip))return Forbid();
        await using var tx=await db.Database.BeginTransactionAsync();
        if(await db.Users.AnyAsync())return Conflict(new{message="L’initialisation est déjà terminée."});
        var user=new AppUser{FullName=r.Name.Trim(),UserName=r.Email.Trim().ToLowerInvariant(),Email=r.Email.Trim().ToLowerInvariant(),EmailConfirmed=true};
        var result=await users.CreateAsync(user,r.Password);
        if(!result.Succeeded)return BadRequest(new{message=string.Join(" ",result.Errors.Select(e=>e.Description))});
        var assigned=await users.AddToRoleAsync(user,"Administrateur");if(!assigned.Succeeded)throw new InvalidOperationException("Attribution du rôle impossible.");
        await audit.WriteAsync("Initialisation du premier administrateur","Utilisateurs",target:user.Id,actor:user.Email);await tx.CommitAsync();return Ok(new{message="Administrateur créé. Connectez-vous."});
    }
    [AllowAnonymous,HttpPost("login"),EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginRequest r){
        var user=await users.FindByEmailAsync(r.Email.Trim());
        if(user==null||!user.IsActive){await audit.WriteAsync("Connexion refusée","Authentification","Échec",actor:"Compte non authentifié");return Unauthorized(new{message="Identifiants invalides ou compte temporairement indisponible."});}
        var result=await signin.PasswordSignInAsync(user,r.Password,false,true);
        await audit.WriteAsync(result.RequiresTwoFactor?"Second facteur demandé":result.Succeeded?"Connexion":"Tentative de connexion","Authentification",result.Succeeded||result.RequiresTwoFactor?"Succès":result.IsLockedOut?"Bloqué":"Échec",user.Id,user.Email);
        if(result.RequiresTwoFactor)return Ok(new{requiresTwoFactor=true});
        if(!result.Succeeded)return Unauthorized(new{message="Identifiants invalides ou compte temporairement indisponible."});
        return Ok(new{requiresTwoFactor=false});
    }
    [AllowAnonymous,HttpPost("twofactor"),EnableRateLimiting("authentication")]
    public async Task<IActionResult> TwoFactor(CodeRequest r){
        var user=await signin.GetTwoFactorAuthenticationUserAsync();if(user==null||!user.IsActive)return Unauthorized(new{message="Recommencez la connexion."});
        var result=r.Recovery?await signin.TwoFactorRecoveryCodeSignInAsync(r.Code.Trim()):await signin.TwoFactorAuthenticatorSignInAsync(r.Code.Replace(" ","").Replace("-",""),false,false);
        await audit.WriteAsync("Vérification du second facteur","Authentification",result.Succeeded?"Succès":result.IsLockedOut?"Bloqué":"Échec",user.Id,user.Email);
        if(!result.Succeeded)return Unauthorized(new{message="Code incorrect, expiré ou compte verrouillé."});return Ok(new{message="Connexion établie."});
    }
    [Authorize,HttpPost("logout")]
    public async Task<IActionResult> Logout(){await audit.WriteAsync("Déconnexion","Authentification");await signin.SignOutAsync();return Ok(new{message="Session fermée."});}
}
