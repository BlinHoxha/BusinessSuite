using System.Threading.RateLimiting;
using BusinessSuite.Api.Middleware;
using BusinessSuite.Api.Security;
using BusinessSuite.Application;
using BusinessSuite.Application.Portfolio;
using BusinessSuite.Domain.Audit;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using BusinessSuite.Infrastructure;
using BusinessSuite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args.Where(a => a is not ("--migrate" or "--bootstrap-owner")).ToArray());
builder.Logging.ClearProviders().AddJsonConsole();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRequestContext, RequestContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddBusinessSuiteInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c => c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new() { Title = "BusinessSuite Backend", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new() { [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];
if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience)))
    throw new InvalidOperationException("Authentication:Authority and Authentication:Audience are required outside Development.");
if (!string.IsNullOrWhiteSpace(authority) && (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https"))
    throw new InvalidOperationException("Authentication:Authority must be an HTTPS URL.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    if (!string.IsNullOrWhiteSpace(authority)) o.Authority = authority;
    if (!string.IsNullOrWhiteSpace(audience)) o.Audience = audience;
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPartition.GetFixedWindowLimiter(http.User.FindFirst("tenant_id")?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddHealthChecks().AddCheck<DatabaseReadiness>("database", tags: ["ready"]);

var app = builder.Build();

// Explicit operator commands; never execute migrations or grant access during normal startup.
if (args.Contains("--migrate") || args.Contains("--bootstrap-owner"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
    if (args.Contains("--migrate")) await db.Database.MigrateAsync();
    if (args.Contains("--bootstrap-owner"))
    {
        var config = builder.Configuration;
        var tenantId = Guid.Parse(config["Bootstrap:TenantId"] ?? throw new InvalidOperationException("Bootstrap:TenantId is required."));
        var userId = config["Bootstrap:UserId"] ?? throw new InvalidOperationException("Bootstrap:UserId is required.");
        if (!await db.Tenants.AnyAsync(x => x.Id == tenantId))
            db.Tenants.Add(new Tenant(tenantId, config["Bootstrap:TenantName"] ?? "Initial tenant"));
        if (!await db.Memberships.AnyAsync(x => x.TenantId == tenantId && x.UserId == userId && x.Scope == AccessScope.Tenant && x.ScopeId == tenantId))
        {
            var membership = new Membership(tenantId, userId, AccessScope.Tenant, tenantId, MemberRole.Owner, DateTimeOffset.UtcNow);
            db.Memberships.Add(membership);
            db.AuditEvents.Add(new AuditEvent(tenantId, "operator-bootstrap", "membership.bootstrapped", membership.Id, DateTimeOffset.UtcNow, Guid.NewGuid().ToString()));
        }
        await db.SaveChangesAsync();
    }
    return;
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/v1/reference-data/currencies", () => Framework.Contracts.Common.CommonResult<IReadOnlyCollection<string>>.Success(CurrencyCode.All));
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();
app.Run();

public partial class Program;

public sealed class DatabaseReadiness(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
            return await db.Database.CanConnectAsync(ct) && !(await db.Database.GetPendingMigrationsAsync(ct)).Any()
                ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database is unavailable or requires migrations.");
        }
        catch { return HealthCheckResult.Unhealthy("Database is unavailable."); }
    }
}
