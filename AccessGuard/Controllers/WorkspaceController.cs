using AccessGuard.Data;
using AccessGuard.Security;
using AccessGuard.Models;
using AccessGuard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;
namespace AccessGuard.Controllers;
[ApiController,Authorize,Route("api")]
public class WorkspaceController(AppDbContext db,UserManager<AppUser> users,IAuthorizationService auth,AuditService audit) : ControllerBase {
    private async Task<bool> Allowed(string p)=>(await auth.AuthorizeAsync(User,p)).Succeeded;
    private async Task<object> UserDto(AppUser u)=>new{id=u.Id,name=u.FullName,email=u.Email,role=(await users.GetRolesAsync(u)).FirstOrDefault()??"Employé",status=u.IsActive?"Actif":"Désactivé",mfa=u.TwoFactorEnabled,version=u.ConcurrencyStamp};
    [HttpGet("workspace")]
    public async Task<IActionResult> State(){
        var me=(await users.GetUserAsync(User))!;var permissions=new List<string>();foreach(var p in Permissions.All)if(await Allowed(p))permissions.Add(p);
        var visible=permissions.Contains("users.read")?await db.Users.OrderBy(x=>x.FullName).ToListAsync():[me];
        visible=visible.OrderBy(x=>x.Id==me.Id?0:1).ToList();var dtos=new List<object>();foreach(var u in visible)dtos.Add(await UserDto(u));
        var matrix=Permissions.Roles.Select(_=>new bool[6]).ToList();
        if(permissions.Contains("roles.write")){var grants=await db.RolePermissions.ToListAsync();matrix=Permissions.Roles.Select(r=>Permissions.All.Select(p=>grants.Any(g=>g.Role==r&&g.Permission==p)).ToArray()).ToList();}
        var events=permissions.Contains("audit.read")?await db.AuditEvents.AsNoTracking().OrderByDescending(x=>x.Id).Take(500).ToListAsync():[];
        var canReadAudit=permissions.Contains("audit.read");
        return Ok(new{users=dtos,profile=new{name=me.FullName,email=me.Email,role=(await users.GetRolesAsync(me)).FirstOrDefault(),id=me.Id},matrix,logs=events.Select(LogDto),permissions,scope=permissions.Contains("users.read")?"organisation":"personnel",meta=new{auditTotal=canReadAudit?await db.AuditEvents.CountAsync():0,alerts=canReadAudit?await db.AuditEvents.CountAsync(x=>x.Result!="Succès"):0,bruteForceAlerts=canReadAudit?await AuditIncidents.Alerts(db).CountAsync():0}});
    }
    [HttpPost("users"),Authorize(Policy="users.write")]
    public async Task<IActionResult> Create(UserRequest r){
        if(!Permissions.Roles.Contains(r.Role)||!new[]{"Actif","Désactivé"}.Contains(r.Status))return BadRequest(new{message="Rôle ou statut invalide."});
        if(r.Role!="Employé"&&!await Allowed("roles.assign"))return Forbid();
        if(string.IsNullOrWhiteSpace(r.Password))return BadRequest(new{message="Un mot de passe initial est requis."});
        await using var tx=await db.Database.BeginTransactionAsync();
        var u=new AppUser{FullName=r.Name.Trim(),Email=r.Email.Trim().ToLowerInvariant(),UserName=r.Email.Trim().ToLowerInvariant(),EmailConfirmed=true,IsActive=r.Status=="Actif"};
        var result=await users.CreateAsync(u,r.Password);if(!result.Succeeded)return BadRequest(new{message=string.Join(" ",result.Errors.Select(x=>x.Description))});
        var assigned=await users.AddToRoleAsync(u,r.Role);if(!assigned.Succeeded)throw new InvalidOperationException("Attribution du rôle impossible.");
        await audit.WriteAsync("Création de compte","Utilisateurs",target:u.Id);await tx.CommitAsync();return Ok(new{message="Utilisateur créé."});
    }
    [HttpPut("users/{id}"),Authorize(Policy="users.write")]
    public async Task<IActionResult> Update(string id,UserRequest r){
        var u=await users.FindByIdAsync(id);if(u==null)return NotFound();
        if(!Permissions.Roles.Contains(r.Role)||!new[]{"Actif","Désactivé"}.Contains(r.Status))return BadRequest(new{message="Rôle ou statut invalide."});
        if(u.ConcurrencyStamp!=r.Version)return Conflict(new{message="Ce compte a été modifié. Rechargez la liste."});
        var current=(await users.GetRolesAsync(u)).FirstOrDefault()??"Employé";
        if(current!=r.Role&&!await Allowed("roles.assign"))return Forbid();
        if(u.Id==users.GetUserId(User)&&(r.Role!=current||r.Status!="Actif"))return BadRequest(new{message="Vous ne pouvez pas retirer votre propre accès administrateur."});
        await using var tx=await db.Database.BeginTransactionAsync();
        if(current=="Administrateur"&&(r.Role!="Administrateur"||r.Status!="Actif")){
            var admins=await users.GetUsersInRoleAsync("Administrateur");if(!admins.Any(x=>x.Id!=u.Id&&x.IsActive))return BadRequest(new{message="Un administrateur actif doit être conservé."});
        }
        u.FullName=r.Name.Trim();u.Email=r.Email.Trim().ToLowerInvariant();u.UserName=u.Email;u.IsActive=r.Status=="Actif";
        var result=await users.UpdateAsync(u);if(!result.Succeeded)return BadRequest(new{message=string.Join(" ",result.Errors.Select(x=>x.Description))});
        if(current!=r.Role){var remove=await users.RemoveFromRoleAsync(u,current);var add=await users.AddToRoleAsync(u,r.Role);if(!remove.Succeeded||!add.Succeeded)throw new InvalidOperationException("Modification du rôle impossible.");}
        await users.UpdateSecurityStampAsync(u);if(u.Id==users.GetUserId(User))await HttpContext.RequestServices.GetRequiredService<SignInManager<AppUser>>().RefreshSignInAsync(u);
        await audit.WriteAsync("Modification de compte","Utilisateurs",target:u.Id);await tx.CommitAsync();return Ok(new{message="Utilisateur enregistré."});
    }
    [HttpPut("roles"),Authorize(Policy="roles.write")]
    public async Task<IActionResult> Roles(MatrixRequest r){
        if(r.Matrix?.Length!=3||r.Matrix.Any(x=>x==null||x.Length!=6))return BadRequest(new{message="Matrice invalide."});
        if(r.Matrix[0].Any(x=>!x))return BadRequest(new{message="Les permissions du rôle Administrateur doivent être conservées."});
        await using var tx=await db.Database.BeginTransactionAsync();db.RolePermissions.RemoveRange(await db.RolePermissions.ToListAsync());
        for(var i=0;i<3;i++)for(var j=0;j<6;j++)if(r.Matrix[i][j])db.RolePermissions.Add(new RolePermission{Role=Permissions.Roles[i],Permission=Permissions.All[j]});
        await audit.WriteAsync("Modification des permissions","Rôles");await tx.CommitAsync();return Ok(new{message="Permissions enregistrées."});
    }
    private static object LogDto(AuditEvent e)=>new{id=e.Id,time=DateTime.SpecifyKind(e.Time,DateTimeKind.Utc),user=e.Actor,action=e.Action,resource=e.Resource,result=e.Result,blocked=e.Result=="Bloqué",ip=e.Ip};
    private IQueryable<AuditEvent> Filter(string? query,string? result,string? from,string? to){
        var q=db.AuditEvents.AsNoTracking();
        if(!string.IsNullOrWhiteSpace(query)){var s=SearchText.Normalize(query);q=q.Where(e=>e.SearchText.Contains(s)||e.Ip.Contains(s));}
        if(result=="unsuccessful")q=q.Where(e=>e.Result!="Succès");else if(!string.IsNullOrEmpty(result))q=q.Where(e=>e.Result==result);
        if(DateTime.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var start))q=q.Where(e=>e.Time>=start);
        if(DateTime.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var end)){end=end.AddDays(1);q=q.Where(e=>e.Time<end);}
        return q;
    }
    [HttpGet("audit"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> Events(string? query,string? result,string? from,string? to){var q=Filter(query,result,from,to);var total=await q.CountAsync();var events=await q.OrderByDescending(e=>e.Id).Take(500).ToListAsync();return Ok(new{total,logs=events.Select(LogDto)});}
    [HttpGet("audit/incidents"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> Incidents([FromServices] AuditIncidents incidents) => Ok(await incidents.GetOverviewAsync());

    [HttpGet("audit/incidents/{id:long}"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> Incident(long id, [FromServices] AuditIncidents incidents, [FromServices] IncidentWorkflow workflow)
    {
        var detail = await incidents.GetAlertAsync(id);
        if (detail is null) return NotFound(new { message = "Incident introuvable." });
        var review = await workflow.GetReviewAsync(id, detail.Incident.Reason != AuditIncidents.IpReason,
            users.GetUserId(User), User.IsInRole(IncidentWorkflow.Administrator));
        return Ok(detail with { Review = review });
    }

    [HttpGet("audit/queue"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> IncidentQueue([FromServices] IncidentWorkflow workflow, string? status, string? severity,
        [System.ComponentModel.DataAnnotations.MaxLength(120)] string? query, int page = 1) =>
        Ok(await workflow.GetQueueAsync(status, severity, query, page));

    [HttpPost("audit/incidents/{id:long}/review"),Authorize(Policy="audit.read", Roles=IncidentWorkflow.Administrator)]
    public async Task<IActionResult> ReviewIncident(long id, IncidentReviewRequest request, [FromServices] IncidentWorkflow workflow)
    {
        var actor = await users.GetUserAsync(User);
        if (actor is null) return Forbid();
        return await workflow.UpdateAsync(id, request, actor.Id, actor.FullName) switch
        {
            IncidentWorkflow.ReviewResult.Saved => Ok(new { message = "Incident enregistré." }),
            IncidentWorkflow.ReviewResult.Unchanged => Ok(new { message = "Aucune modification." }),
            IncidentWorkflow.ReviewResult.Missing => NotFound(new { message = "Incident introuvable." }),
            IncidentWorkflow.ReviewResult.Conflict => Conflict(new { message = "Cet incident a été modifié. Rouvrez-le pour voir sa dernière version." }),
            IncidentWorkflow.ReviewResult.NoteRequired => BadRequest(new { message = "Ajoutez une note pour expliquer la résolution." }),
            _ => BadRequest(new { message = "Vérifiez le statut, la gravité et l’affectation." })
        };
    }

    [HttpGet("audit/metrics"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> SecurityMetrics([FromServices] SecurityAnalytics metrics) => Ok(await metrics.GetAsync());

    [HttpGet("audit/incidents/accounts/{id}"),Authorize(Policy="audit.read")]
    public async Task<IActionResult> AccountIncident(string id, [FromServices] AuditIncidents incidents) =>
        await incidents.GetAccountBlockAsync(id) is { } detail ? Ok(detail) : NotFound(new { message = "Ce compte n’est plus verrouillé." });

    [HttpGet("audit/export"),Authorize(Policy="audit.export")]
    public async Task<IActionResult> Export(string? query,string? result,string? from,string? to){
        var events=await Filter(query,result,from,to).OrderByDescending(e=>e.Id).Take(10000).ToListAsync();
        static string Cell(string s)=>"\""+(s.Length>0&&"=+-@\t\r".Contains(s[0])?"'":"")+s.Replace("\"","\"\"")+"\"";
        var csv=new StringBuilder("Date UTC;Utilisateur;Événement;Ressource;Résultat;Adresse IP\r\n");foreach(var e in events)csv.AppendLine(string.Join(';',new[]{e.Time.ToString("O"),e.Actor,e.Action,e.Resource,e.Result,e.Ip}.Select(Cell)));
        await audit.WriteAsync("Export du journal","Audit");return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv; charset=utf-8","accessguard-audit.csv");
    }
}
