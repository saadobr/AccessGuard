using AccessGuard.Data;
using AccessGuard.Models;
using Microsoft.EntityFrameworkCore;
using static AccessGuard.Services.BruteForceProtection;

namespace AccessGuard.Services;

public sealed class IncidentWorkflow(AppDbContext db, AuditService audit)
{
    private const int PageSize = 20;
    public const string Administrator = "Administrateur";
    private static readonly string[] Statuses = ["nouveau", "en-cours", "resolu"];
    private static readonly string[] Severities = ["faible", "moyenne", "elevee", "critique"];

    public async Task<Queue> GetQueueAsync(string? status, string? severity, string? search, int page)
    {
        var latest = db.IncidentReviews.GroupBy(r => r.AlertId).Select(g => g.Max(r => r.Id));
        var queue = from alert in AuditIncidents.Alerts(db)
                    join current in db.IncidentReviews.AsNoTracking().Where(r => latest.Contains(r.Id))
                        on alert.Id equals current.AlertId into reviews
                    from review in reviews.DefaultIfEmpty()
                    select new
                    {
                        alert.Id, alert.Time, alert.SearchText,
                        Target = alert.Action == DistributedAlertAction ? alert.Actor : alert.Ip,
                        alert.Action,
                        Status = review == null ? "nouveau" : review.Status,
                        Severity = review == null ? (alert.Action == AlertAction ? "moyenne" : "elevee") : review.Severity,
                        Owner = review == null ? null : review.Owner
                    };
        if (Statuses.Contains(status)) queue = queue.Where(item => item.Status == status);
        if (Severities.Contains(severity)) queue = queue.Where(item => item.Severity == severity);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var filter = Security.SearchText.Normalize(search);
            queue = queue.Where(item => item.SearchText.Contains(filter) || item.Target.Contains(filter));
        }

        var total = await queue.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
        page = Math.Clamp(page, 1, pages);
        var result = await queue.OrderByDescending(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return new Queue(total, page, pages, result.Select(x => new QueueItem(x.Id, AsUtc(x.Time), x.Target,
            AuditIncidents.Reason(x.Action),
            x.Status, x.Severity, x.Owner)).ToArray());
    }

    public async Task<Review> GetReviewAsync(long id, bool highSeverity, string? userId, bool canManage)
    {
        var query = db.IncidentReviews.AsNoTracking().Where(r => r.AlertId == id).OrderByDescending(r => r.Id);
        var updates = await query.Take(50).ToListAsync();
        var current = Current(updates.FirstOrDefault(), highSeverity);
        return new Review(current.Id, current.Status, current.Severity, current.Owner, current.OwnerId == userId && userId != null, canManage,
            updates.Select(r => new Activity(AsUtc(r.Time), r.Actor, r.Status, r.Severity, r.Owner, r.Note)).ToArray());
    }

    public async Task<ReviewResult> UpdateAsync(long id, IncidentReviewRequest request, string userId, string actor)
    {
        if (!Statuses.Contains(request.Status) || !Severities.Contains(request.Severity)
            || request.Assignment is not ("keep" or "claim" or "release"))
            return ReviewResult.Invalid;

        // SQLite serializes writers. Check the revision after acquiring its write transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var alert = await AuditIncidents.Alerts(db).SingleOrDefaultAsync(e => e.Id == id);
        if (alert is null) return ReviewResult.Missing;
        var previous = await db.IncidentReviews.AsNoTracking().Where(r => r.AlertId == id)
            .OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        var current = Current(previous, alert.Action != AlertAction);
        if (current.Id != request.Version) return ReviewResult.Conflict;

        var note = request.Note?.Trim() ?? "";
        if (request.Status == "resolu" && current.Status != "resolu" && note.Length == 0)
            return ReviewResult.NoteRequired;

        var (ownerId, owner) = request.Assignment switch
        {
            "claim" => (userId, actor),
            "release" => (null, null),
            _ => (current.OwnerId, current.Owner)
        };
        if (request.Status == current.Status && request.Severity == current.Severity && ownerId == current.OwnerId && note.Length == 0)
            return ReviewResult.Unchanged;

        db.IncidentReviews.Add(new IncidentReview
        {
            AlertId = id, Actor = actor, Status = request.Status, Severity = request.Severity,
            OwnerId = ownerId, Owner = owner, Note = note
        });
        await audit.WriteAsync("Mise à jour du triage", "Incidents", target: id.ToString(System.Globalization.CultureInfo.InvariantCulture), actor: actor);
        await transaction.CommitAsync();
        return ReviewResult.Saved;
    }

    private static IncidentReview Current(IncidentReview? current, bool highSeverity) =>
        current ?? new IncidentReview { Severity = highSeverity ? "elevee" : "moyenne" };

    private static DateTimeOffset AsUtc(DateTime time) => new(DateTime.SpecifyKind(time, DateTimeKind.Utc));

    public enum ReviewResult { Saved, Invalid, Missing, Conflict, NoteRequired, Unchanged }
    public sealed record Queue(int Total, int Page, int Pages, QueueItem[] Items);
    public sealed record QueueItem(long Id, DateTimeOffset Time, string Target, string Reason, string Status, string Severity, string? Owner);
    public sealed record Review(long Version, string Status, string Severity, string? Owner, bool IsMine, bool CanManage, Activity[] History);
    public sealed record Activity(DateTimeOffset Time, string Actor, string Status, string Severity, string? Owner, string Note);
}
