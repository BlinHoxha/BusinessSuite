using BusinessSuite.Application;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Api.Security;

public sealed class RequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private HttpContext Http => accessor.HttpContext ?? throw new InvalidOperationException("No active request.");
    public Guid TenantId => Guid.TryParse(Http.User.FindFirst("tenant_id")?.Value, out var id) && id != Guid.Empty
        ? id : throw new DomainException("A valid tenant_id claim is required.", 403);
    public string UserId => Http.User.FindFirst("sub")?.Value is { Length: > 0 and <= 200 } id
        ? id : throw new DomainException("A valid subject claim is required.", 403);
    public string CorrelationId => Http.TraceIdentifier;
}
