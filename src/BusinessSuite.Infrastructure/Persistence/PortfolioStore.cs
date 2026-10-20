using BusinessSuite.Application;
using BusinessSuite.Domain.Audit;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using Framework.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BusinessSuite.Infrastructure.Persistence;

public sealed class PortfolioStore(BusinessSuiteDbContext db, IRequestContext context) : IPortfolioStore
{
    public async Task<IReadOnlyList<Membership>> Memberships(Guid tenantId, string userId, CancellationToken ct) =>
        await db.Memberships.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).ToListAsync(ct);
    public Task<Portfolio?> Portfolio(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Portfolios.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public Task<LegalEntity?> LegalEntity(Guid tenantId, Guid id, CancellationToken ct) =>
        db.LegalEntities.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public Task<Branch?> Branch(Guid tenantId, Guid id, CancellationToken ct) =>
        db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task<IReadOnlyList<Portfolio>> Portfolios(Guid tenantId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct) =>
        await db.Portfolios.AsNoTracking().Where(x => x.TenantId == tenantId && (visibleIds == null || visibleIds.Contains(x.Id)))
            .OrderBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public async Task<IReadOnlyList<LegalEntity>> LegalEntities(Guid tenantId, Guid portfolioId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct) =>
        await db.LegalEntities.AsNoTracking().Where(x => x.TenantId == tenantId && x.PortfolioId == portfolioId && (visibleIds == null || visibleIds.Contains(x.Id)))
            .OrderBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public async Task<IReadOnlyList<Branch>> Branches(Guid tenantId, Guid entityId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(x => x.TenantId == tenantId && x.LegalEntityId == entityId && (visibleIds == null || visibleIds.Contains(x.Id)))
            .OrderBy(x => x.Id).Skip(skip).Take(take).ToListAsync(ct);
    public async Task Create(TenantRecord record, AuditEvent audit, CancellationToken ct)
    {
        if (record.TenantId != context.TenantId || audit.TenantId != context.TenantId || audit.ActorId != context.UserId || audit.ResourceId != record.Id)
            throw new DomainException("Write scope is invalid.", 403);
        db.Add(record);
        db.AuditEvents.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { throw new DomainException("A record with that identifier already exists.", 409); }
    }
}
