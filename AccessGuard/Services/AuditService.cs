using AccessGuard.Data;
using System.Security.Claims;
namespace AccessGuard.Services;
public class AuditService(AppDbContext db,IHttpContextAccessor accessor) {
    internal string? ClientIp {
        get {
            var ip=accessor.HttpContext?.Connection.RemoteIpAddress;
            return ip?.IsIPv4MappedToIPv6==true?ip.MapToIPv4().ToString():ip?.ToString();
        }
    }
    public async Task WriteAsync(string action,string resource,string result="Succès",string target="",string? actor=null,DateTime? time=null,DateTime? incidentWindowStart=null) {
        var h=accessor.HttpContext;
        db.AuditEvents.Add(new AuditEvent{Time=time??DateTime.UtcNow,ActorId=h?.User.FindFirstValue(ClaimTypes.NameIdentifier)??"",Actor=actor??h?.User.Identity?.Name??"Anonyme",Action=action,Resource=resource,Result=result,TargetId=target,Ip=ClientIp??"",IncidentWindowStart=incidentWindowStart});
        await db.SaveChangesAsync();
    }
}
