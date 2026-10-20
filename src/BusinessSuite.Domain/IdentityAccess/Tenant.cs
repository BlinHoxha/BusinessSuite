using Framework.Domain.Abstractions;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Domain.IdentityAccess;

public sealed class Tenant : BaseEntity<Guid>
{
    public string Name { get; private set; } = "";
    private Tenant() { }
    public Tenant(Guid id, string name)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new DomainException("Tenant ID and a name of at most 200 characters are required.");
        Id = id;
        Name = name.Trim();
    }
}
