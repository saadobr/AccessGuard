using AccessGuard.Data;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
namespace AccessGuard.Services;
public class AuditService(AppDbContext db,IHttpContextAccessor accessor) {
    public async Task WriteAsync(string action,string resource,string result="Succès",string target="",string? actor=null) {
        var h=accessor.HttpContext;
        db.AuditEvents.Add(new AuditEvent{ActorId=h?.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",Actor=actor??h?.User.Identity?.Name??"Anonyme",Action=action,Resource=resource,Result=result,TargetId=target,Ip=h?.Connection.RemoteIpAddress?.ToString()??""});
        await db.SaveChangesAsync();
    }
}
