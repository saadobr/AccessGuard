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
    public DateTime? IncidentWindowStart { get; set; }
}
public class IncidentReview {
    public long Id { get; set; }
    public long AlertId { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "";
    public string Status { get; set; } = "nouveau";
    public string Severity { get; set; } = "moyenne";
    public string? OwnerId { get; set; }
    public string? Owner { get; set; }
    public string Note { get; set; } = "";
}
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options) {
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<IncidentReview> IncidentReviews => Set<IncidentReview>();
    protected override void OnModelCreating(ModelBuilder b) {
        base.OnModelCreating(b);
        b.Entity<AppUser>().Property(x=>x.FullName).HasMaxLength(80);
        b.Entity<RolePermission>().HasKey(x=>new{x.Role,x.Permission});
        b.Entity<AuditEvent>().HasIndex(x=>x.Time);
        b.Entity<AuditEvent>().HasIndex(x=>x.ActorId);
        b.Entity<IncidentReview>().HasIndex(x=>new{x.AlertId,x.Id});
        b.Entity<IncidentReview>().HasOne<AuditEvent>().WithMany().HasForeignKey(x=>x.AlertId).OnDelete(DeleteBehavior.Restrict);
    }
    public override Task<int> SaveChangesAsync(CancellationToken token=default) {
        if(ChangeTracker.Entries().Any(e=>(e.Entity is AuditEvent or IncidentReview) && (e.State is EntityState.Modified or EntityState.Deleted)))
            throw new InvalidOperationException("Le journal est en ajout seul.");
        foreach(var e in ChangeTracker.Entries<AuditEvent>().Where(e=>e.State==EntityState.Added)) e.Entity.SearchText=AccessGuard.Security.SearchText.Normalize(e.Entity.Actor+" "+e.Entity.Action+" "+e.Entity.Resource);
        return base.SaveChangesAsync(token);
    }
}
