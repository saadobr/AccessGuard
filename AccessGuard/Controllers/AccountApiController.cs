using AccessGuard.Data;
using AccessGuard.Models;
using AccessGuard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace AccessGuard.Controllers;
[ApiController,Authorize,Route("api/account"),EnableRateLimiting("authentication")]
public class AccountApiController(UserManager<AppUser> users,SignInManager<AppUser> signin,AuditService audit) : ControllerBase {
    [HttpPost("profile")]
    public async Task<IActionResult> Profile(ProfileRequest r){
        var user=(await users.GetUserAsync(User))!;
        if(!await users.CheckPasswordAsync(user,r.Password))return BadRequest(new{message="Mot de passe actuel incorrect."});
        var email=r.Email.Trim().ToLowerInvariant();var existing=await users.FindByEmailAsync(email);
        if(existing!=null&&existing.Id!=user.Id)return Conflict(new{message="Cette adresse est déjà utilisée."});
        user.FullName=r.Name.Trim();user.Email=email;user.UserName=email;
        var result=await users.UpdateAsync(user);if(!result.Succeeded)return BadRequest(new{message=string.Join(" ",result.Errors.Select(x=>x.Description))});
        await users.UpdateSecurityStampAsync(user);await signin.RefreshSignInAsync(user);await audit.WriteAsync("Modification du profil","Compte",target:user.Id);return Ok(new{message="Profil enregistré."});
    }
    [HttpPost("twofactor/prepare")]
    public async Task<IActionResult> Prepare(PasswordRequest r){
        var user=(await users.GetUserAsync(User))!;if(!await users.CheckPasswordAsync(user,r.Password))return BadRequest(new{message="Mot de passe actuel incorrect."});
        if(user.TwoFactorEnabled)return Conflict(new{message="La 2FA est déjà activée. Désactivez-la avant de remplacer la clé."});
        // A prepare request reuses a pending key; it never invalidates an enabled authenticator.
        if(await users.GetAuthenticatorKeyAsync(user)==null)await users.ResetAuthenticatorKeyAsync(user);
        var key=await users.GetAuthenticatorKeyAsync(user);
        await signin.RefreshSignInAsync(user);await audit.WriteAsync("Préparation de la 2FA","Compte",target:user.Id);
        return Ok(new{secret=key,uri=$"otpauth://totp/{Uri.EscapeDataString("AccessGuard:"+user.Email)}?secret={key}&issuer=AccessGuard&digits=6&period=30"});
    }
    [HttpPost("twofactor/enable")]
    public async Task<IActionResult> Enable(CodeRequest r){
        var user=(await users.GetUserAsync(User))!;if(user.TwoFactorEnabled)return Conflict(new{message="La 2FA est déjà activée."});
        if(!await users.VerifyTwoFactorTokenAsync(user,users.Options.Tokens.AuthenticatorTokenProvider,r.Code.Replace(" ","").Replace("-","")))return BadRequest(new{message="Code invalide ou expiré."});
        await users.SetTwoFactorEnabledAsync(user,true);var codes=await users.GenerateNewTwoFactorRecoveryCodesAsync(user,10);await users.UpdateSecurityStampAsync(user);await signin.RefreshSignInAsync(user);await audit.WriteAsync("Activation de la 2FA","Compte",target:user.Id);return Ok(new{message="2FA activée.",recoveryCodes=codes});
    }
    [HttpPost("twofactor/disable")]
    public async Task<IActionResult> Disable(SensitiveRequest r){
        var user=(await users.GetUserAsync(User))!;
        if(!await users.CheckPasswordAsync(user,r.Password)||!await users.VerifyTwoFactorTokenAsync(user,users.Options.Tokens.AuthenticatorTokenProvider,r.Code.Replace(" ","")))return BadRequest(new{message="Mot de passe ou code incorrect."});
        await users.SetTwoFactorEnabledAsync(user,false);await users.ResetAuthenticatorKeyAsync(user);await users.GenerateNewTwoFactorRecoveryCodesAsync(user,0);await users.UpdateSecurityStampAsync(user);await signin.RefreshSignInAsync(user);await audit.WriteAsync("Désactivation de la 2FA","Compte",target:user.Id);return Ok(new{message="2FA désactivée."});
    }
}
