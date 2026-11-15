using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace AccessGuard.Data;

public class AppUser : IdentityUser {
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public class RolePermission {
    public string Role { get; set; } = "";
    public string Permission { get; set; } = "";
}
public class AuditEvent {
    public long Id { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string ActorId { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public string Result { get; set; } = "";
    public string Ip { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string SearchText { get; set; } = "";
}
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options) {
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    protected override void OnModelCreating(ModelBuilder b) {
        base.OnModelCreating(b);
        b.Entity<AppUser>().Property(x=>x.FullName).HasMaxLength(80);
        b.Entity<RolePermission>().HasKey(x=>new{x.Role,x.Permission});
        b.Entity<AuditEvent>().HasIndex(x=>x.Time);
        b.Entity<AuditEvent>().HasIndex(x=>x.ActorId);
    }
    public override Task<int> SaveChangesAsync(CancellationToken token=default) {
        if(ChangeTracker.Entries<AuditEvent>().Any(e=>e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Le journal est en ajout seul.");
        foreach(var e in ChangeTracker.Entries<AuditEvent>().Where(e=>e.State==EntityState.Added)) e.Entity.SearchText=AccessGuard.Security.SearchText.Normalize(e.Entity.Actor+" "+e.Entity.Action+" "+e.Entity.Resource);
        return base.SaveChangesAsync(token);
    }
}
