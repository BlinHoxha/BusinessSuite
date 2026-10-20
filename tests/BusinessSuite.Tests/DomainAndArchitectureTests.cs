using BusinessSuite.Domain.IdentityAccess;
using BusinessSuite.Domain.Portfolio;
using Framework.Domain.Exceptions;

namespace BusinessSuite.Tests;

public sealed class DomainAndArchitectureTests
{
    [Fact]
    public void Domain_validates_currency_fiscal_calendar_and_timezone()
    {
        var tenant = Guid.NewGuid(); var portfolio = Guid.NewGuid();
        Assert.Throws<DomainException>(() => new LegalEntity(tenant, portfolio, "Company", "CH", "1", "CHF", "de-CH", 13));
        Assert.Throws<DomainException>(() => new Branch(tenant, Guid.NewGuid(), "Branch", "Not/AZone", "Address"));
        Assert.Throws<DomainException>(() => CurrencyCode.Validate("ZZZ"));
        Assert.Equal("CHF", CurrencyCode.Validate(" chf "));
    }
    [Fact]
    public void Membership_cannot_reference_another_tenant_as_its_tenant_scope() =>
        Assert.Throws<DomainException>(() => new Membership(Guid.NewGuid(), "User", AccessScope.Tenant, Guid.NewGuid(), MemberRole.Owner, DateTimeOffset.UtcNow));
    [Theory]
    [InlineData("BusinessSuite.Domain")]
    [InlineData("BusinessSuite.Contracts")]
    [InlineData("BusinessSuite.Application")]
    public void Inner_layers_do_not_reference_host_or_persistence(string name)
    {
        var references = System.Reflection.Assembly.Load(name).GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("BusinessSuite.Api", references);
        Assert.DoesNotContain("BusinessSuite.Infrastructure", references);
        Assert.DoesNotContain("Framework.Infrastructure", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        if (name == "BusinessSuite.Domain") Assert.DoesNotContain("BusinessSuite.Application", references);
        if (name == "BusinessSuite.Contracts") Assert.DoesNotContain("BusinessSuite.Domain", references);
    }
}
