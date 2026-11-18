using AccessGuard.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static AccessGuard.Services.BruteForceProtection;

namespace AccessGuard.Services;

public sealed class AuditIncidents(AppDbContext db, IOptions<BruteForceOptions> options, IOptions<IdentityOptions> identity)
{
    internal const string IpReason = "Brute force par IP";
    internal const string DistributedReason = "Brute force distribuée";
    private const string SprayReason = "Password spraying suspecté";
    private const string AccountReason = "Échecs répétés sur le compte";
    private const int AttemptLimit = 100;
    private readonly BruteForceOptions settings = options.Value;

    public async Task<Overview> GetOverviewAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.UtcDateTime.AddSeconds(-settings.BlockSeconds);
        var recentAlerts = await Alerts(db).OrderByDescending(e => e.Id).Take(50).ToListAsync();
        var activeAlerts = await Alerts(db).Where(e => e.Time > cutoff).OrderByDescending(e => e.Id).ToListAsync();
        var blocks = activeAlerts.Where(e => e.Action != DistributedAlertAction).DistinctBy(e => e.Ip)
            .Select(e => new ActiveBlock(Reason(e.Action), e.Ip, AsUtc(e.Time).AddSeconds(settings.BlockSeconds), e.Id, null)).ToList();

        // SQLite cannot order or compare DateTimeOffset via EF. Compare the projected lock dates in memory.
        var candidates = await LockedAccounts().Select(u => new { u.Id, u.Email, u.UserName, u.LockoutEnd }).ToListAsync();
        var distributedAlerts = activeAlerts.Where(e => e.Action == DistributedAlertAction)
            .DistinctBy(e => e.TargetId).ToDictionary(e => e.TargetId);
        foreach (var account in candidates.Where(u => u.LockoutEnd > now))
        {
            distributedAlerts.TryGetValue(account.Id, out var alert);
            blocks.Add(new ActiveBlock(alert is null ? AccountReason : DistributedReason,
                account.Email ?? account.UserName ?? account.Id, account.LockoutEnd!.Value, alert?.Id, account.Id));
        }

        return new Overview(now, blocks.OrderBy(b => b.ExpiresAt).ToArray(), recentAlerts.Select(Summarize).ToArray());
    }

    public async Task<Detail?> GetAlertAsync(long id)
    {
        var alert = await Alerts(db).SingleOrDefaultAsync(e => e.Id == id);
        if (alert is null) return null;

        var candidates = db.AuditEvents.AsNoTracking().Where(e => e.Id < alert.Id && e.Time <= alert.Time);
        var windowStart = alert.Time.AddSeconds(-settings.WindowSeconds);
        if (alert.Action == DistributedAlertAction)
        {
            var since = alert.IncidentWindowStart ?? (await LatestAlertAsync(candidates,
                alert.TargetId, windowStart, distributed: true))?.AddTicks(1) ?? windowStart;
            return await BuildDetailAsync(Summarize(alert), DistributedFailures(candidates, alert.TargetId, since));
        }

        var ipSince = alert.IncidentWindowStart;
        if (ipSince is null)
        {
            var previousIpAlert = await LatestAlertAsync(candidates, alert.Ip,
                windowStart.AddSeconds(-settings.BlockSeconds), distributed: false);
            var previousExpiry = previousIpAlert?.AddSeconds(settings.BlockSeconds);
            ipSince = previousExpiry > windowStart ? previousExpiry.Value : windowStart;
        }
        var failures = IpFailures(candidates, alert.Ip, ipSince.Value);
        return await BuildDetailAsync(Summarize(alert), alert.Action == SprayAlertAction ? SprayFailures(failures) : failures);
    }

    public async Task<Detail?> GetAccountBlockAsync(string id)
    {
        var account = await LockedAccounts().Select(u => new { u.Id, u.Email, u.UserName, u.LockoutEnd })
            .SingleOrDefaultAsync(u => u.Id == id);
        if (account?.LockoutEnd is not DateTimeOffset expiry || expiry <= DateTimeOffset.UtcNow) return null;

        var lockStart = expiry - identity.Value.Lockout.DefaultLockoutTimeSpan;
        var attempts = db.AuditEvents.AsNoTracking().Where(e => e.TargetId == id && e.Resource == AuthResource
            && (e.Action == PasswordAttempt || e.Action == SecondFactorAttempt)
            && (e.Result == "Échec" || e.Result == "Bloqué")
            && e.Time >= lockStart.UtcDateTime.AddSeconds(-settings.WindowSeconds));
        return await BuildDetailAsync(new Summary(null, AccountReason, account.Email ?? account.UserName ?? id, lockStart), attempts);
    }

    private IQueryable<AppUser> LockedAccounts() => db.Users.AsNoTracking()
        .Where(u => u.IsActive && u.LockoutEnabled && u.LockoutEnd != null);

    internal static IQueryable<AuditEvent> Alerts(AppDbContext db) => db.AuditEvents.AsNoTracking()
        .Where(e => e.Resource == AuthResource && (e.Action == AlertAction || e.Action == DistributedAlertAction || e.Action == SprayAlertAction));

    private static Summary Summarize(AuditEvent e) => e.Action == DistributedAlertAction
        ? new(e.Id, DistributedReason, e.Actor, AsUtc(e.Time))
        : new(e.Id, Reason(e.Action), e.Ip, AsUtc(e.Time));

    internal static string Reason(string action) => action switch
    {
        DistributedAlertAction => DistributedReason,
        SprayAlertAction => SprayReason,
        _ => IpReason
    };

    private static async Task<Detail> BuildDetailAsync(Summary incident, IQueryable<AuditEvent> attempts)
    {
        var count = await attempts.CountAsync();
        var rows = await attempts.OrderByDescending(e => e.Id).Take(AttemptLimit).ToListAsync();
        var ips = await attempts.Where(e => e.Ip != "").Select(e => e.Ip).Distinct().OrderBy(ip => ip).ToArrayAsync();
        var accounts = await attempts.Where(e => e.TargetId != "").Select(e => e.Actor).Distinct().OrderBy(actor => actor).ToArrayAsync();
        return new Detail(incident, count, ips, accounts, rows.Select(e => new Attempt(
            AsUtc(e.Time), e.Actor, e.Action, e.Resource, e.Result, e.Ip)).ToArray());
    }

    private static DateTimeOffset AsUtc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));

    public sealed record Overview(DateTimeOffset CheckedAt, ActiveBlock[] Blocks, Summary[] Incidents);
    public sealed record ActiveBlock(string Reason, string Target, DateTimeOffset ExpiresAt, long? IncidentId, string? AccountId);
    public sealed record Summary(long? Id, string Reason, string Target, DateTimeOffset Time);
    public sealed record Detail(Summary Incident, int Total, string[] Ips, string[] Accounts, Attempt[] Attempts, IncidentWorkflow.Review? Review = null);
    public sealed record Attempt(DateTimeOffset Time, string User, string Action, string Resource, string Result, string Ip);
}
