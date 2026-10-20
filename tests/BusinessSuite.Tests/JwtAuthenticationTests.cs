using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace BusinessSuite.Tests;

public sealed class JwtAuthenticationTests
{
    [Theory]
    [InlineData("businesssuite-tests", "businesssuite-api", true, true, HttpStatusCode.OK)]
    [InlineData("wrong-issuer", "businesssuite-api", true, true, HttpStatusCode.Unauthorized)]
    [InlineData("businesssuite-tests", "wrong-audience", true, true, HttpStatusCode.Unauthorized)]
    [InlineData("businesssuite-tests", "businesssuite-api", false, true, HttpStatusCode.Unauthorized)]
    [InlineData("businesssuite-tests", "businesssuite-api", true, false, HttpStatusCode.Forbidden)]
    public async Task Signed_tokens_validate_issuer_audience_expiry_and_tenant_claim(string issuer, string audience, bool current, bool tenantClaim, HttpStatusCode expected)
    {
        using var factory = new ApiFactory(realJwt: true); await factory.Seed(); using var client = factory.Client(null);
        var claims = new List<Claim> { new("sub", "owner") };
        if (tenantClaim) claims.Add(new("tenant_id", ApiFactory.TenantA.ToString()));
        var token = new JwtSecurityToken(issuer, audience, claims, DateTime.UtcNow.AddHours(-2),
            current ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddHours(-1), new SigningCredentials(ApiFactory.SigningKey, SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(expected, (await client.GetAsync("/api/v1/portfolios")).StatusCode);
    }
}
