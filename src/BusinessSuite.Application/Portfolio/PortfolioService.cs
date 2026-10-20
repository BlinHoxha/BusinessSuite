using BusinessSuite.Contracts.Portfolio;
using BusinessSuite.Domain.Audit;
using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Application.Portfolio;

public sealed class PortfolioService(IPortfolioStore store, IRequestContext context, TimeProvider clock)
{
    private async Task<IReadOnlyList<Membership>> Grants(CancellationToken ct) =>
        (await store.Memberships(context.TenantId, context.UserId, ct)).Where(m => m.IsActive(clock.GetUtcNow())).ToArray();

    private bool Allows(IEnumerable<Membership> grants, bool write, Guid? portfolio = null, Guid? entity = null, Guid? branch = null) =>
        grants.Any(m => (!write || m.Role is MemberRole.Owner or MemberRole.Manager) &&
            ((m.Scope == AccessScope.Tenant && m.ScopeId == context.TenantId) ||
             (m.Scope == AccessScope.Portfolio && m.ScopeId == portfolio) ||
             (m.Scope == AccessScope.LegalEntity && m.ScopeId == entity) ||
             (m.Scope == AccessScope.Branch && m.ScopeId == branch)));

    private static DomainException Hidden() => new("Resource was not found.", 404);
    private static void Page(int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 100) throw new DomainException("Offset must be nonnegative and limit must be 1-100.");
    }
    private Task Save(TenantRecord record, string action, CancellationToken ct) => store.Create(record,
        new AuditEvent(context.TenantId, context.UserId, action, record.Id, clock.GetUtcNow(), context.CorrelationId), ct);

    public async Task<PortfolioResponse> CreatePortfolio(CreatePortfolioRequest request, CancellationToken ct)
    {
        if (!Allows(await Grants(ct), true)) throw new DomainException("Tenant management permission is required.", 403);
        var record = new Domain.Portfolio.Portfolio(context.TenantId, request.DisplayName, request.ReportingCurrency);
        await Save(record, "portfolio.created", ct);
        return Map(record);
    }
    public async Task<PortfolioResponse> GetPortfolio(Guid id, CancellationToken ct)
    {
        var record = await store.Portfolio(context.TenantId, id, ct) ?? throw Hidden();
        if (!Allows(await Grants(ct), false, id)) throw Hidden();
        return Map(record);
    }
    public async Task<IReadOnlyList<PortfolioResponse>> ListPortfolios(int offset, int limit, CancellationToken ct)
    {
        Page(offset, limit);
        var grants = await Grants(ct);
        var ids = Allows(grants, false) ? null : grants.Where(m => m.Scope == AccessScope.Portfolio).Select(m => m.ScopeId).ToArray();
        return (await store.Portfolios(context.TenantId, ids, offset, limit, ct)).Select(Map).ToArray();
    }
    public async Task<LegalEntityResponse> CreateLegalEntity(CreateLegalEntityRequest request, CancellationToken ct)
    {
        _ = await store.Portfolio(context.TenantId, request.PortfolioId, ct) ?? throw Hidden();
        if (!Allows(await Grants(ct), true, request.PortfolioId)) throw new DomainException("Portfolio management permission is required.", 403);
        var record = new LegalEntity(context.TenantId, request.PortfolioId, request.RegisteredName, request.CountryCode,
            request.RegistrationNumber, request.FunctionalCurrency, request.Locale, request.FiscalYearStartMonth);
        await Save(record, "legal-entity.created", ct);
        return Map(record);
    }
    public async Task<LegalEntityResponse> GetLegalEntity(Guid id, CancellationToken ct)
    {
        var record = await store.LegalEntity(context.TenantId, id, ct) ?? throw Hidden();
        if (!Allows(await Grants(ct), false, record.PortfolioId, id)) throw Hidden();
        return Map(record);
    }
    public async Task<IReadOnlyList<LegalEntityResponse>> ListLegalEntities(Guid portfolioId, int offset, int limit, CancellationToken ct)
    {
        Page(offset, limit);
        _ = await store.Portfolio(context.TenantId, portfolioId, ct) ?? throw Hidden();
        var grants = await Grants(ct);
        var ids = Allows(grants, false, portfolioId) ? null : grants.Where(m => m.Scope == AccessScope.LegalEntity).Select(m => m.ScopeId).ToArray();
        return (await store.LegalEntities(context.TenantId, portfolioId, ids, offset, limit, ct)).Select(Map).ToArray();
    }
    public async Task<BranchResponse> CreateBranch(CreateBranchRequest request, CancellationToken ct)
    {
        var parent = await store.LegalEntity(context.TenantId, request.LegalEntityId, ct) ?? throw Hidden();
        if (!Allows(await Grants(ct), true, parent.PortfolioId, parent.Id)) throw new DomainException("Entity management permission is required.", 403);
        var record = new Branch(context.TenantId, parent.Id, request.DisplayName, request.TimeZone, request.Address);
        await Save(record, "branch.created", ct);
        return Map(record);
    }
    public async Task<BranchResponse> GetBranch(Guid id, CancellationToken ct)
    {
        var record = await store.Branch(context.TenantId, id, ct) ?? throw Hidden();
        var parent = await store.LegalEntity(context.TenantId, record.LegalEntityId, ct) ?? throw Hidden();
        if (!Allows(await Grants(ct), false, parent.PortfolioId, parent.Id, id)) throw Hidden();
        return Map(record);
    }
    public async Task<IReadOnlyList<BranchResponse>> ListBranches(Guid entityId, int offset, int limit, CancellationToken ct)
    {
        Page(offset, limit);
        var parent = await store.LegalEntity(context.TenantId, entityId, ct) ?? throw Hidden();
        var grants = await Grants(ct);
        var ids = Allows(grants, false, parent.PortfolioId, entityId) ? null : grants.Where(m => m.Scope == AccessScope.Branch).Select(m => m.ScopeId).ToArray();
        return (await store.Branches(context.TenantId, entityId, ids, offset, limit, ct)).Select(Map).ToArray();
    }
    private static PortfolioResponse Map(Domain.Portfolio.Portfolio p) => new(p.Id, p.DisplayName, p.ReportingCurrency);
    private static LegalEntityResponse Map(LegalEntity e) => new(e.Id, e.PortfolioId, e.RegisteredName, e.CountryCode, e.RegistrationNumber, e.FunctionalCurrency, e.Locale, e.FiscalYearStartMonth);
    private static BranchResponse Map(Branch b) => new(b.Id, b.LegalEntityId, b.DisplayName, b.TimeZone, b.Address);
}
