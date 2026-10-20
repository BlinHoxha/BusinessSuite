using BusinessSuite.Domain.Portfolio;

namespace BusinessSuite.Domain.Audit;

public sealed class AuditEvent : TenantRecord
{
    public string ActorId { get; private set; } = "";
    public string Action { get; private set; } = "";
    public Guid ResourceId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string CorrelationId { get; private set; } = "";
    private AuditEvent() { }
    public AuditEvent(Guid tenantId, string actor, string action, Guid resourceId, DateTimeOffset at, string correlationId) : base(tenantId)
    {
        ActorId = Required(actor, 200);
        Action = Required(action, 100);
        ResourceId = resourceId;
        OccurredAt = at;
        CorrelationId = Required(correlationId, 100);
    }
}
