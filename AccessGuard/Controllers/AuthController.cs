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
public class AuthController(AppDbContext db,UserManager<AppUser> users,SignInManager<AppUser> signin,AuditService audit,IAntiforgery csrf,IWebHostEnvironment env,BruteForceProtection bruteForce) : ControllerBase {
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
        if(await bruteForce.GetActiveBlockAsync() is DateTime blockedUntil)return IpBlocked(blockedUntil);

        const string message="Identifiants invalides ou compte temporairement indisponible.";
        var user=await users.FindByEmailAsync(r.Email.Trim());
        if(user==null||!user.IsActive){
            var expiry=await bruteForce.RecordFailureAsync(BruteForceProtection.RejectedLogin,"Échec",actor:"Compte non authentifié");
            return expiry is DateTime block?IpBlocked(block):Unauthorized(new{message});
        }

        var result=await signin.PasswordSignInAsync(user,r.Password,false,true);
        if(!result.Succeeded&&!result.RequiresTwoFactor){
            var expiry=await bruteForce.RecordFailureAsync(BruteForceProtection.PasswordAttempt,result.IsLockedOut?"Bloqué":"Échec",user.Id,user.Email);
            return expiry is DateTime block?IpBlocked(block):Unauthorized(new{message});
        }

        await audit.WriteAsync(result.RequiresTwoFactor?"Second facteur demandé":"Connexion",BruteForceProtection.AuthResource,target:user.Id,actor:user.Email);
        return Ok(new{requiresTwoFactor=result.RequiresTwoFactor});
    }
    [AllowAnonymous,HttpPost("twofactor"),EnableRateLimiting("authentication")]
    public async Task<IActionResult> TwoFactor(CodeRequest r){
        if(await bruteForce.GetActiveBlockAsync() is DateTime blockedUntil)return IpBlocked(blockedUntil);

        var user=await signin.GetTwoFactorAuthenticationUserAsync();if(user==null||!user.IsActive)return Unauthorized(new{message="Recommencez la connexion."});
        var result=r.Recovery?await signin.TwoFactorRecoveryCodeSignInAsync(r.Code.Trim()):await signin.TwoFactorAuthenticatorSignInAsync(r.Code.Replace(" ","").Replace("-",""),false,false);
        if(!result.Succeeded){
            var expiry=await bruteForce.RecordFailureAsync(BruteForceProtection.SecondFactorAttempt,result.IsLockedOut?"Bloqué":"Échec",user.Id,user.Email);
            return expiry is DateTime block?IpBlocked(block):Unauthorized(new{message="Code incorrect, expiré ou compte verrouillé."});
        }

        await audit.WriteAsync(BruteForceProtection.SecondFactorAttempt,BruteForceProtection.AuthResource,target:user.Id,actor:user.Email);
        return Ok(new{message="Connexion établie."});
    }

    private ObjectResult IpBlocked(DateTime until){
        var retryAfter=Math.Max(1,(int)Math.Ceiling((until-DateTime.UtcNow).TotalSeconds));
        Response.Headers.RetryAfter=retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status429TooManyRequests,new{message="Trop de tentatives de connexion. Réessayez dans quelques minutes.",retryAfter});
    }
    [Authorize,HttpPost("logout")]
    public async Task<IActionResult> Logout(){await audit.WriteAsync("Déconnexion","Authentification");await signin.SignOutAsync();return Ok(new{message="Session fermée."});}
}
