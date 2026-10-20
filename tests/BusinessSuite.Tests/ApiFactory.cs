using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using BusinessSuite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BusinessSuite.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly bool realJwt;
    public static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("BusinessSuite-test-only-signing-key-1234567890"));
    public static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public Guid PortfolioA { get; private set; }
    public Guid EntityA { get; private set; }
    public Guid BranchA { get; private set; }
    public Guid BranchB { get; private set; }
    public Guid PortfolioB { get; private set; }
    public ApiFactory(bool realJwt = false) { this.realJwt = realJwt; connection.Open(); }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused;Database=unused");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "Host=unused;Database=unused" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BusinessSuiteDbContext>();
            services.RemoveAll<DbContextOptions<BusinessSuiteDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<BusinessSuiteDbContext>>();
            services.AddDbContext<BusinessSuiteDbContext>(o => o.UseSqlite(connection));
            if (realJwt)
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    o.TokenValidationParameters = new()
                    {
                        ValidIssuer = "businesssuite-tests", ValidAudience = "businesssuite-api",
                        IssuerSigningKey = SigningKey, ValidateIssuerSigningKey = true,
                        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                    };
                });
            }
            else
                services.AddAuthentication(o => { o.DefaultAuthenticateScheme = "Test"; o.DefaultChallengeScheme = "Test"; })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        });
    }
    public async Task Seed()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
        await db.Database.EnsureCreatedAsync();
        var portfolio = new Portfolio(TenantA, "Owner A", "EUR");
        var other = new Portfolio(TenantB, "Owner B", "CHF");
        var entity = new LegalEntity(TenantA, portfolio.Id, "Company", "XK", "123", "EUR", "sq-XK", 1);
        var a = new Branch(TenantA, entity.Id, "A", "Europe/Budapest", "A address");
        var b = new Branch(TenantA, entity.Id, "B", "Europe/Zurich", "B address");
        PortfolioA = portfolio.Id; PortfolioB = other.Id; EntityA = entity.Id; BranchA = a.Id; BranchB = b.Id;
        db.AddRange(new Tenant(TenantA, "A"), new Tenant(TenantB, "B"), portfolio, other, entity, a, b);
        var now = DateTimeOffset.UtcNow.AddDays(-1);
        db.AddRange(
            new Membership(TenantA, "owner", AccessScope.Tenant, TenantA, MemberRole.Owner, now),
            new Membership(TenantB, "owner", AccessScope.Tenant, TenantB, MemberRole.Owner, now),
            new Membership(TenantA, "branch-manager", AccessScope.Branch, a.Id, MemberRole.Manager, now),
            new Membership(TenantA, "accountant", AccessScope.LegalEntity, entity.Id, MemberRole.Accountant, now),
            new Membership(TenantA, "auditor", AccessScope.Portfolio, portfolio.Id, MemberRole.Auditor, now),
            new Membership(TenantA, "expired", AccessScope.Tenant, TenantA, MemberRole.Owner, now, DateTimeOffset.UtcNow.AddHours(-1)),
            new Membership(TenantA, "future", AccessScope.Tenant, TenantA, MemberRole.Owner, DateTimeOffset.UtcNow.AddDays(1)));
        await db.SaveChangesAsync();
    }
    public HttpClient Client(string? user = "owner", Guid? tenant = null)
    {
        var client = CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", user);
            client.DefaultRequestHeaders.Add("X-Test-Tenant", (tenant ?? TenantA).ToString());
        }
        return client;
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) connection.Dispose(); }
}

// This handler is compiled only in the test project and cannot be enabled in the API.
public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-User", out var user)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new[] { new Claim("sub", user.ToString()), new Claim("tenant_id", Request.Headers["X-Test-Tenant"].ToString()) };
        return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")), "Test")));
    }
}
