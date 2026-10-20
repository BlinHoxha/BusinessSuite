using BusinessSuite.Domain.Audit;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;

namespace BusinessSuite.Application;

public interface IRequestContext
{
    Guid TenantId { get; }
    string UserId { get; }
    string CorrelationId { get; }
}

// No unscoped IQueryable or generic CRUD path is exposed to application consumers.
public interface IPortfolioStore
{
    Task<IReadOnlyList<Membership>> Memberships(Guid tenantId, string userId, CancellationToken ct);
    Task<Domain.Portfolio.Portfolio?> Portfolio(Guid tenantId, Guid id, CancellationToken ct);
    Task<LegalEntity?> LegalEntity(Guid tenantId, Guid id, CancellationToken ct);
    Task<Branch?> Branch(Guid tenantId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<Domain.Portfolio.Portfolio>> Portfolios(Guid tenantId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<LegalEntity>> LegalEntities(Guid tenantId, Guid portfolioId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<Branch>> Branches(Guid tenantId, Guid entityId, IReadOnlyCollection<Guid>? visibleIds, int skip, int take, CancellationToken ct);
    Task Create(TenantRecord record, AuditEvent audit, CancellationToken ct);
}
