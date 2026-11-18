using AccessGuard.Data;
using AccessGuard.Security;
using AccessGuard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder=WebApplication.CreateBuilder(args);
var dataPath=builder.Configuration["AccessGuard:DataPath"]??Path.Combine(builder.Environment.ContentRootPath,"App_Data");
Directory.CreateDirectory(dataPath);
builder.Services.AddDbContext<AppDbContext>(o=>o.UseSqlite(builder.Configuration.GetConnectionString("Default")??$"Data Source={Path.Combine(dataPath,"accessguard.db")}"));
var protection=builder.Services.AddDataProtection().SetApplicationName("AccessGuard").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataPath,"keys")));
if(OperatingSystem.IsWindows())protection.ProtectKeysWithDpapi();
builder.Services.AddIdentity<AppUser,IdentityRole>(o=>{
    o.User.RequireUniqueEmail=true;o.Password.RequiredLength=12;o.Password.RequireDigit=true;o.Password.RequireUppercase=true;o.Password.RequireLowercase=true;o.Password.RequireNonAlphanumeric=true;
    o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);o.Lockout.MaxFailedAccessAttempts=5;o.Lockout.AllowedForNewUsers=true;
}).AddEntityFrameworkStores<AppDbContext>().AddUserStore<ProtectedUserStore>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o=>{
    o.LoginPath="/Portal/Login";o.AccessDeniedPath="/Home/Status?code=403";
    o.Cookie.Name="AccessGuard.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;
    o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
    o.ExpireTimeSpan=TimeSpan.FromMinutes(30);o.SlidingExpiration=true;
    o.Events.OnRedirectToLogin=c=>{if(c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=401;return c.Response.WriteAsJsonAsync(new{message="Connexion requise."});}c.Response.Redirect(c.RedirectUri);return Task.CompletedTask;};
    o.Events.OnRedirectToAccessDenied=c=>{if(c.Request.Path.StartsWithSegments("/api")){c.Response.StatusCode=403;return c.Response.WriteAsJsonAsync(new{message="Permission insuffisante."});}c.Response.Redirect(c.RedirectUri);return Task.CompletedTask;};
    o.Events.OnValidatePrincipal=async c=>{
        await SecurityStampValidator.ValidatePrincipalAsync(c);
        if(c.Principal?.Identity?.IsAuthenticated==true){var users=c.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();var user=await users.GetUserAsync(c.Principal);if(user==null||!user.IsActive||await users.IsLockedOutAsync(user)){c.RejectPrincipal();await c.HttpContext.RequestServices.GetRequiredService<SignInManager<AppUser>>().SignOutAsync();}}
    };
});
builder.Services.Configure<SecurityStampValidatorOptions>(o=>o.ValidationInterval=TimeSpan.Zero);
builder.Services.AddAuthorization(o=>{
    o.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach(var p in Permissions.All)o.AddPolicy(p,new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(p)).Build());
});
builder.Services.AddScoped<IAuthorizationHandler,PermissionHandler>();
builder.Services.AddScoped<AuditService>();builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BruteForceProtection>();
builder.Services.AddScoped<AuditIncidents>();
builder.Services.AddScoped<IncidentWorkflow>();
builder.Services.AddScoped<SecurityAnalytics>();
builder.Services.AddOptions<BruteForceOptions>().BindConfiguration("AccessGuard:BruteForce")
    .Validate(o=>o.MaxFailures>0&&o.WindowSeconds>0&&o.BlockSeconds>0,"Les seuils et durées de protection doivent être positifs.")
    .Validate(o=>o.DistributedMinIps>=2&&o.DistributedMaxFailures>=o.DistributedMinIps,"La détection distribuée doit couvrir au moins deux IP et autant d’échecs que d’IP.")
    .Validate(o=>o.SprayMinAccounts>=2,"La détection multi-comptes doit couvrir au moins deux comptes.")
    .ValidateOnStart();
builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="AccessGuard.Csrf";o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;});
builder.Services.AddControllersWithViews(o=>o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.OnRejected=async(c,t)=>{c.HttpContext.Response.StatusCode=429;c.HttpContext.Response.Headers.RetryAfter="60";await c.HttpContext.Response.WriteAsJsonAsync(new{message="Trop de tentatives. Réessayez dans une minute."},t);};o.AddPolicy("authentication",h=>RateLimitPartition.GetFixedWindowLimiter(h.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();
await DbInitializer.InitializeAsync(app.Services);
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
app.Use(async(h,next)=>{h.Response.Headers["X-Content-Type-Options"]="nosniff";h.Response.Headers["Referrer-Policy"]="same-origin";h.Response.Headers["X-Frame-Options"]="DENY";h.Response.Headers.CacheControl="no-store";await next();});
app.UseStatusCodePagesWithReExecute("/Home/Status","?code={0}");
if(!app.Environment.IsDevelopment())app.UseHttpsRedirection();
app.UseRouting();app.UseRateLimiter();app.UseAuthentication();
app.Use(async(h,next)=>{await next();if(h.Response.StatusCode==403&&h.User.Identity?.IsAuthenticated==true&&h.Request.Path.StartsWithSegments("/api"))await h.RequestServices.GetRequiredService<AuditService>().WriteAsync("Accès refusé",h.Request.Path,"Refusé");});
app.UseAuthorization();
app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute("default","{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();
public partial class Program { }
