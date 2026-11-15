using AccessGuard.Data;
using AccessGuard.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AccessGuard.Services;
public static class DbInitializer {
    public static async Task InitializeAsync(IServiceProvider services) {
        using var scope=services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach(var role in Permissions.Roles)if(!await roles.RoleExistsAsync(role)) {
            var created=await roles.CreateAsync(new IdentityRole(role));
            if(!created.Succeeded)throw new InvalidOperationException("Création des rôles impossible.");
        }
        // Only seed permissions on the first schema initialization, not on every restart.
        if(!await db.RolePermissions.AnyAsync()) {
            foreach(var p in Permissions.All)db.RolePermissions.Add(new RolePermission{Role="Administrateur",Permission=p});
            foreach(var p in new[]{"users.read","audit.read","audit.export"})db.RolePermissions.Add(new RolePermission{Role="Auditeur",Permission=p});
            await db.SaveChangesAsync();
        }
        var config=scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var email=config["Bootstrap:Email"];var password=config["Bootstrap:Password"];
        if(!await db.Users.AnyAsync()&&!string.IsNullOrWhiteSpace(email)&&!string.IsNullOrWhiteSpace(password)) {
            var manager=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var admin=new AppUser{Email=email.Trim(),UserName=email.Trim(),FullName=config["Bootstrap:Name"]??"Administrateur",EmailConfirmed=true};
            var result=await manager.CreateAsync(admin,password);
            if(!result.Succeeded)throw new InvalidOperationException("Le compte initial configuré ne respecte pas les règles Identity.");
            var assigned=await manager.AddToRoleAsync(admin,"Administrateur");
            if(!assigned.Succeeded)throw new InvalidOperationException("Attribution du rôle initial impossible.");
            db.AuditEvents.Add(new AuditEvent{Actor="Initialisation",Action="Création du premier administrateur",Resource="Utilisateurs",Result="Succès",TargetId=admin.Id});await db.SaveChangesAsync();
        }
        if(!scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment()&&!await db.Users.AnyAsync())
            throw new InvalidOperationException("Configurez Bootstrap__Email et Bootstrap__Password pour le premier démarrage en production.");
    }
}
