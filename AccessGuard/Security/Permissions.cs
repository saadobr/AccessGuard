using AccessGuard.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace AccessGuard.Security;
public static class Permissions {
    public static readonly string[] Roles=["Administrateur","Auditeur","Employé"];
    public static readonly string[] All=["users.read","users.write","roles.assign","audit.read","audit.export","roles.write"];
}
public record PermissionRequirement(string Permission) : IAuthorizationRequirement;
public class PermissionHandler(AppDbContext db) : AuthorizationHandler<PermissionRequirement> {
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext c,PermissionRequirement r) {
        var id=c.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if(id==null||!await db.Users.AnyAsync(u=>u.Id==id&&u.IsActive))return;
        var names=from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId==id select role.Name;
        if(await db.RolePermissions.AnyAsync(p=>names.Contains(p.Role)&&p.Permission==r.Permission))c.Succeed(r);
    }
}
