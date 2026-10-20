using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessSuite.Contracts.Portfolio;
using BusinessSuite.Domain.Portfolio;
using BusinessSuite.Infrastructure.Persistence;
using Framework.Contracts.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessSuite.Tests;

public sealed class AccessAndWorkflowTests
{
    [Fact]
    public async Task Owner_can_create_hierarchy_and_each_write_is_audited()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client();
        var created = await client.PostAsJsonAsync("/api/v1/portfolios", new CreatePortfolioRequest("Global", "EUR"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var portfolio = (await created.Content.ReadFromJsonAsync<CommonResult<PortfolioResponse>>())!.Data!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        var entityResult = await client.PostAsJsonAsync("/api/v1/legal-entities", new CreateLegalEntityRequest(portfolio.Id, "Swiss Company", "CH", "CH-987", "CHF", "de-CH"));
        Assert.Equal(HttpStatusCode.Created, entityResult.StatusCode);
        var entity = (await entityResult.Content.ReadFromJsonAsync<CommonResult<LegalEntityResponse>>())!.Data!;
        var branchResult = await client.PostAsJsonAsync("/api/v1/branches", new CreateBranchRequest(entity.Id, "Zurich", "Europe/Zurich", "Example address"));
        Assert.Equal(HttpStatusCode.Created, branchResult.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
        var audit = await db.AuditEvents.ToListAsync();
        Assert.Equal(3, audit.Count);
        Assert.All(audit, a => { Assert.Equal(ApiFactory.TenantA, a.TenantId); Assert.Equal("owner", a.ActorId); Assert.NotEmpty(a.CorrelationId); });
    }
    [Fact]
    public async Task Cross_tenant_reads_lists_and_parent_references_are_isolated()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/portfolios/{factory.PortfolioB}")).StatusCode);
        var list = (await client.GetFromJsonAsync<CommonResult<PortfolioResponse[]>>("/api/v1/portfolios"))!.Data!;
        Assert.Single(list); Assert.Equal(factory.PortfolioA, list[0].Id);
        var create = await client.PostAsJsonAsync("/api/v1/legal-entities", new CreateLegalEntityRequest(factory.PortfolioB, "Attack", "CH", "BAD", "CHF", "de-CH"));
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>().AuditEvents.ToListAsync());
    }
    [Fact]
    public async Task Branch_manager_cannot_read_sibling_branch_or_parent_details_or_create_siblings()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client("branch-manager");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/branches/{factory.BranchA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/branches/{factory.BranchB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/legal-entities/{factory.EntityA}")).StatusCode);
        var list = (await client.GetFromJsonAsync<CommonResult<BranchResponse[]>>($"/api/v1/branches?legalEntityId={factory.EntityA}"))!.Data!;
        Assert.Single(list); Assert.Equal(factory.BranchA, list[0].Id);
        var create = await client.PostAsJsonAsync("/api/v1/branches", new CreateBranchRequest(factory.EntityA, "No", "UTC", "No"));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }
    [Theory]
    [InlineData("accountant")]
    [InlineData("auditor")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("unassigned")]
    public async Task Read_only_inactive_and_unassigned_users_cannot_write(string user)
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client(user);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/portfolios", new CreatePortfolioRequest("Forbidden", "EUR"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/branches", new CreateBranchRequest(factory.EntityA, "Forbidden", "UTC", "Address"))).StatusCode);
    }
    [Fact]
    public async Task Authentication_and_valid_claims_are_required_even_in_development()
    {
        using var factory = new ApiFactory(); await factory.Seed();
        using var anonymous = factory.Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/portfolios")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live")).StatusCode);
        using var badClaim = factory.Client("owner", Guid.Empty);
        Assert.Equal(HttpStatusCode.Forbidden, (await badClaim.GetAsync("/api/v1/portfolios")).StatusCode);
    }
    [Fact]
    public async Task Invalid_requests_return_problem_details_and_do_not_write()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client();
        foreach (var request in new[] { new CreatePortfolioRequest("", "EUR"), new CreatePortfolioRequest("Valid", "ZZZ") })
        {
            var response = await client.PostAsJsonAsync("/api/v1/portfolios", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/portfolios?limit=101")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>().AuditEvents.ToListAsync());
    }
    [Fact]
    public async Task Database_rejects_cross_tenant_parent_even_when_application_is_bypassed()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
        db.LegalEntities.Add(new LegalEntity(ApiFactory.TenantA, factory.PortfolioB, "Invalid", "CH", "Bad", "CHF", "de-CH", 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Failed_persistence_rolls_back_audit_with_the_business_record()
    {
        using var factory = new ApiFactory(); await factory.Seed(); using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/v1/legal-entities", new CreateLegalEntityRequest(factory.PortfolioA, "Duplicate", "XK", "123", "EUR", "sq-XK"));
        // SQLite is test-only; supported database providers map unique violations to 409.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BusinessSuiteDbContext>();
        Assert.Empty(await db.AuditEvents.ToListAsync());
        Assert.Equal(1, await db.LegalEntities.CountAsync());
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SQLite", body); Assert.DoesNotContain("DbUpdateException", body);
    }
    [Fact]
    public async Task OpenApi_includes_core_routes_without_exposing_framework_reference_endpoints()
    {
        using var factory = new ApiFactory(); using var client = factory.Client();
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = spec.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/legal-entities", out _));
        Assert.False(paths.TryGetProperty("/api/v1/knowledge/documents", out _));
    }
}
