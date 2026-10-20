using BusinessSuite.Domain.Audit;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using Framework.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BusinessSuite.Infrastructure.Persistence;

public sealed class BusinessSuiteDbContext(DbContextOptions<BusinessSuiteDbContext> options) : FrameworkDbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<LegalEntity> LegalEntities => Set<LegalEntity>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants", "identity");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
        });
        Configure<Portfolio>(model, "Portfolios", "portfolio");
        Configure<LegalEntity>(model, "LegalEntities", "portfolio");
        Configure<Branch>(model, "Branches", "portfolio");
        Configure<Membership>(model, "Memberships", "identity");
        Configure<AuditEvent>(model, "AuditEvents", "audit");

        model.Entity<Portfolio>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.ReportingCurrency).HasMaxLength(3);
        });
        model.Entity<LegalEntity>(e =>
        {
            e.Property(x => x.RegisteredName).HasMaxLength(200);
            e.Property(x => x.CountryCode).HasMaxLength(2);
            e.Property(x => x.RegistrationNumber).HasMaxLength(100);
            e.Property(x => x.FunctionalCurrency).HasMaxLength(3);
            e.Property(x => x.Locale).HasMaxLength(35);
            e.HasIndex(x => new { x.TenantId, x.CountryCode, x.RegistrationNumber }).IsUnique();
            e.HasOne<Portfolio>().WithMany().HasForeignKey(x => new { x.TenantId, x.PortfolioId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Branch>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.TimeZone).HasMaxLength(100);
            e.Property(x => x.Address).HasMaxLength(500);
            e.HasOne<LegalEntity>().WithMany().HasForeignKey(x => new { x.TenantId, x.LegalEntityId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Membership>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(200);
            e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => new { x.TenantId, x.UserId, x.Scope, x.ScopeId }).IsUnique();
        });
        model.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.ActorId).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.CorrelationId).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });
    }
    private static void Configure<T>(ModelBuilder model, string table, string schema) where T : TenantRecord
    {
        model.Entity<T>(e =>
        {
            e.ToTable(table, schema);
            e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.TenantId, x.Id });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
