using AccessGuard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccessGuard.Services;

public sealed class BruteForceOptions
{
    public int MaxFailures { get; set; } = 10;
    public int WindowSeconds { get; set; } = 300;
    public int BlockSeconds { get; set; } = 900;
    public int DistributedMaxFailures { get; set; } = 4;
    public int DistributedMinIps { get; set; } = 3;
    public int SprayMinAccounts { get; set; } = 5;
}

public sealed class BruteForceProtection(
    AppDbContext db, AuditService audit, IOptions<BruteForceOptions> options)
{
    public const string AlertAction = "Alerte brute force";
    public const string DistributedAlertAction = "Alerte brute force distribuée";
    public const string SprayAlertAction = "Alerte password spraying suspecté";
    public const string AuthResource = "Authentification";
    public const string RejectedLogin = "Connexion refusée";
    public const string PasswordAttempt = "Tentative de connexion";
    public const string SecondFactorAttempt = "Vérification du second facteur";

    private readonly BruteForceOptions settings = options.Value;

    public async Task<DateTime?> GetActiveBlockAsync()
    {
        if (audit.ClientIp is not { } ip) return null;

        var now = DateTime.UtcNow;
        var latest = await LatestAlertAsync(db.AuditEvents, ip, now.AddSeconds(-settings.BlockSeconds), distributed: false);
        return latest?.AddSeconds(settings.BlockSeconds);
    }

    public async Task<DateTime?> RecordFailureAsync(string action, string result, string target = "", string? actor = null)
    {
        if (audit.ClientIp is not { } ip)
        {
            await audit.WriteAsync(action, AuthResource, result, target, actor);
            return null;
        }

        // SQLite takes the write lock at BeginTransaction, so concurrent failures cannot create duplicate alerts.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var windowStart = now.AddSeconds(-settings.WindowSeconds);
        var latest = await LatestAlertAsync(db.AuditEvents, ip, windowStart.AddSeconds(-settings.BlockSeconds), distributed: false);
        var previousExpiry = latest?.AddSeconds(settings.BlockSeconds);
        await audit.WriteAsync(action, AuthResource, result, target, actor, time: now);
        if (target.Length > 0 && result == "Échec")
            await CheckDistributedFailuresAsync(target, actor, now, windowStart);

        if (previousExpiry > now)
        {
            await transaction.CommitAsync();
            return previousExpiry;
        }

        var since = previousExpiry > windowStart ? previousExpiry.Value : windowStart;
        var recentFailures = IpFailures(db.AuditEvents, ip, since);
        var failures = await recentFailures.Take(settings.MaxFailures).CountAsync();
        var spray = action == PasswordAttempt && result == "Échec" && target.Length > 0
            && await SprayFailures(recentFailures).Select(e => e.TargetId).Distinct().Take(settings.SprayMinAccounts).CountAsync() >= settings.SprayMinAccounts;

        DateTime? expiry = null;
        if (spray || failures >= settings.MaxFailures)
        {
            await audit.WriteAsync(spray ? SprayAlertAction : AlertAction, AuthResource, "Bloqué",
                actor: "AccessGuard", time: now, incidentWindowStart: since);
            expiry = now.AddSeconds(settings.BlockSeconds);
        }

        await transaction.CommitAsync();
        return expiry;
    }

    private async Task CheckDistributedFailuresAsync(string target, string? actor, DateTime now, DateTime windowStart)
    {
        var latestAlert = await LatestAlertAsync(db.AuditEvents, target, windowStart, distributed: true);
        var since = latestAlert?.AddTicks(1) ?? windowStart;
        var failures = DistributedFailures(db.AuditEvents, target, since);

        if (await failures.Take(settings.DistributedMaxFailures).CountAsync() < settings.DistributedMaxFailures
            || await failures.Select(e => e.Ip).Distinct().Take(settings.DistributedMinIps).CountAsync() < settings.DistributedMinIps)
            return;

        var accounts = db.Users.Where(u => u.Id == target && u.IsActive && u.LockoutEnabled);
        var account = await accounts.Select(u => new { u.LockoutEnd }).SingleOrDefaultAsync();
        if (account is null || account.LockoutEnd > now) return;

        var expiry = new DateTimeOffset(now.AddSeconds(settings.BlockSeconds));
        var stamp = Guid.NewGuid().ToString();
        await accounts.ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.LockoutEnd, expiry)
            .SetProperty(u => u.AccessFailedCount, 0)
            .SetProperty(u => u.ConcurrencyStamp, stamp));
        await audit.WriteAsync(DistributedAlertAction, AuthResource, "Bloqué", target, actor, time: now, incidentWindowStart: since);
    }

    internal static IQueryable<AuditEvent> IpFailures(IQueryable<AuditEvent> events, string ip, DateTime since) => events
        .Where(e => e.Ip == ip && e.Time >= since && e.Resource == AuthResource
            && (e.Result == "Échec" || e.Result == "Bloqué")
            && (e.Action == RejectedLogin || e.Action == PasswordAttempt || e.Action == SecondFactorAttempt));

    internal static IQueryable<AuditEvent> DistributedFailures(IQueryable<AuditEvent> events, string target, DateTime since) => events
        .Where(e => e.TargetId == target && e.Time >= since && e.Resource == AuthResource
            && e.Result == "Échec" && e.Ip != "" && (e.Action == PasswordAttempt || e.Action == SecondFactorAttempt));

    internal static IQueryable<AuditEvent> SprayFailures(IQueryable<AuditEvent> failures) => failures
        .Where(e => e.Action == PasswordAttempt && e.Result == "Échec" && e.TargetId != "");

    internal static Task<DateTime?> LatestAlertAsync(IQueryable<AuditEvent> events, string key, DateTime since, bool distributed)
    {
        var alerts = distributed
            ? events.Where(e => e.Action == DistributedAlertAction && e.TargetId == key && e.Time >= since)
            : events.Where(e => (e.Action == AlertAction || e.Action == SprayAlertAction) && e.Ip == key && e.Time > since);
        return alerts.OrderByDescending(e => e.Time).Select(e => (DateTime?)e.Time).FirstOrDefaultAsync();
    }
}
