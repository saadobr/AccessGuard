using AccessGuard.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
namespace AccessGuard.Security;
// Protect Identity token values (authenticator key and recovery-code store) at rest.
public class ProtectedUserStore : UserStore<AppUser,IdentityRole,AppDbContext,string> {
    private readonly IDataProtector protector;
    public ProtectedUserStore(AppDbContext db,IDataProtectionProvider provider) : base(db) { protector=provider.CreateProtector("AccessGuard.IdentityTokens.v1"); }
    public override Task SetTokenAsync(AppUser user,string loginProvider,string name,string? value,CancellationToken token=default)
        => base.SetTokenAsync(user,loginProvider,name,value==null?null:"encrypted:"+protector.Protect(value),token);
    public override async Task<string?> GetTokenAsync(AppUser user,string loginProvider,string name,CancellationToken token=default) {
        var value=await base.GetTokenAsync(user,loginProvider,name,token);
        return value?.StartsWith("encrypted:")==true?protector.Unprotect(value[10..]):value;
    }
}
