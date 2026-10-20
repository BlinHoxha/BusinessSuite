using BusinessSuite.Domain.Portfolio;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Domain.IdentityAccess;

public enum AccessScope { Tenant, Portfolio, LegalEntity, Branch }
public enum MemberRole { Owner, Manager, Accountant, Operator, Auditor }

public sealed class Membership : TenantRecord
{
    public string UserId { get; private set; } = "";
    public AccessScope Scope { get; private set; }
    public Guid ScopeId { get; private set; }
    public MemberRole Role { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    private Membership() { }
    public Membership(Guid tenantId, string userId, AccessScope scope, Guid scopeId, MemberRole role,
        DateTimeOffset validFrom, DateTimeOffset? validUntil = null) : base(tenantId)
    {
        UserId = Required(userId, 200);
        if (scopeId == Guid.Empty || !Enum.IsDefined(scope) || !Enum.IsDefined(role))
            throw new DomainException("Membership scope and role are invalid.");
        if (scope == AccessScope.Tenant && scopeId != tenantId)
            throw new DomainException("Tenant membership must reference its tenant.");
        if (validUntil <= validFrom) throw new DomainException("Membership validity is invalid.");
        Scope = scope;
        ScopeId = scopeId;
        Role = role;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }
    public bool IsActive(DateTimeOffset now) => ValidFrom <= now && (ValidUntil is null || ValidUntil > now);
}
