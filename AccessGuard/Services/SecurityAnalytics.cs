using AccessGuard.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using static AccessGuard.Services.BruteForceProtection;

namespace AccessGuard.Services;

public sealed class SecurityAnalytics(AppDbContext db, IOptions<BruteForceOptions> bruteForce, IOptions<IdentityOptions> identity)
{
    public async Task<Overview> GetAsync()
    {
        var now = DateTime.UtcNow;
        var from = now.Date.AddDays(-6);
        var signalDays = await db.AuditEvents.AsNoTracking()
            .Where(e => e.Time >= from && e.Time <= now && e.Resource == AuthResource)
            .GroupBy(e => e.Time.Date)
            .Select(day => new
            {
                Date = day.Key,
                Accepted = day.Count(e => e.Result == "Succès" && (e.Action == "Connexion" || e.Action == SecondFactorAttempt)),
                Refused = day.Count(e => (e.Result == "Échec" || e.Result == "Bloqué")
                    && (e.Action == RejectedLogin || e.Action == PasswordAttempt || e.Action == SecondFactorAttempt)),
                Alerts = day.Count(e => e.Action == AlertAction || e.Action == DistributedAlertAction || e.Action == SprayAlertAction)
            }).ToDictionaryAsync(day => day.Date);

        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = from.AddDays(offset);
            signalDays.TryGetValue(date, out var counts);
            return new Day(DateOnly.FromDateTime(date), counts?.Accepted ?? 0, counts?.Refused ?? 0, counts?.Alerts ?? 0);
        }).ToArray();

        var active = db.Users.AsNoTracking().Where(u => u.IsActive);
        var admins = from u in active
                     join membership in db.UserRoles on u.Id equals membership.UserId
                     join role in db.Roles on membership.RoleId equals role.Id
                     where role.Name == IncidentWorkflow.Administrator
                     select u;
        var coverage = new Coverage(await active.CountAsync(), await active.CountAsync(u => u.TwoFactorEnabled),
            await admins.CountAsync(), await admins.CountAsync(u => u.TwoFactorEnabled));
        var protection = bruteForce.Value;
        var lockout = identity.Value.Lockout;
        return new Overview(days, coverage, new Policy(protection.MaxFailures, protection.WindowSeconds, protection.BlockSeconds,
            protection.DistributedMaxFailures, protection.DistributedMinIps, protection.SprayMinAccounts, lockout.MaxFailedAccessAttempts,
            (int)lockout.DefaultLockoutTimeSpan.TotalSeconds));
    }

    public sealed record Day(DateOnly Date, int Accepted, int Refused, int Alerts);
    public sealed record Coverage(int Active, int Protected, int Admins, int ProtectedAdmins);
    public sealed record Policy(int IpFailures, int WindowSeconds, int BlockSeconds, int DistributedFailures,
        int DistributedIps, int SprayAccounts, int AccountFailures, int AccountBlockSeconds);
    public sealed record Overview(Day[] Days, Coverage Coverage, Policy Policy);
}
