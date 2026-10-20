# BusinessSuite backend

Backend foundation for the AI-Powered Multi-Business Management & Financial Intelligence Platform. ASP.NET Core modular monolith with Clean Architecture, consuming the existing sibling **Framework** libraries through project references. No frontend is included.

This increment implements the hierarchy and access portion of the foundation. It is not the complete Phase 1 or an operational finance product. See [architecture and delivery scope](docs/ARCHITECTURE.md) for decisions and remaining work.

## Included

- JWT authentication in every environment, validated tenant/subject claims, and database-backed scoped memberships.
- Create, read and paginated list APIs for portfolios, legal entities and branches.
- Reporting/functional currency, legal jurisdiction, locale, fiscal year start and branch timezone validation.
- PostgreSQL persistence, module schemas, composite tenant/parent foreign keys, initial EF migration and atomic creation/audit writes.
- CommonResult responses from Framework, Problem Details errors, OpenAPI, structured JSON logs, rate limiting and health probes.
- Unit, architecture and relational HTTP integration tests; CI template and container packaging.

## Workspace and build

Keep the repositories side by side:

```text
repos/
  AI-Framework/
  Framework/
  BusinessSuite/
```

Install the .NET 9 SDK used by Framework. From BusinessSuite:

```powershell
dotnet restore BusinessSuite.sln
dotnet build BusinessSuite.sln --no-restore
dotnet test BusinessSuite.sln --no-build --no-restore
```

Open `BusinessSuite.sln` in Visual Studio and select `BusinessSuite.Api` as the startup project. Project references build the shared libraries automatically. Framework source and its reference API remain unchanged. Its central package baseline is imported by `Directory.Packages.props`.

**Shared dependencies:** BusinessSuite selects Framework.Application.Abstractions and Framework.Persistence.PostgreSql. Its runtime no longer inherits AutoMapper or SQL Server. The legacy Framework mapping adapter still has advisory GHSA-rvv3-g6hj-g44x; it is not suppressed or remediated by this dependency cleanup.

## Local database and initial owner

Create an empty PostgreSQL database named `businesssuite`, using a dedicated local database user. Supply its actual credentials without committing them:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=businesssuite;Username=YOUR_USER;Password=YOUR_PASSWORD'
$env:Bootstrap__TenantId = '11111111-1111-1111-1111-111111111111'
$env:Bootstrap__TenantName = 'My business group'
$env:Bootstrap__UserId = 'local-owner'

dotnet run --project src/BusinessSuite.Api --no-launch-profile -- --migrate --bootstrap-owner
```

The operator command exits after applying migrations and creating the tenant and its first owner membership. Repeating bootstrap does not duplicate an existing membership or elevate an existing lower role. Bootstrap requires trusted operator/database access and is not an HTTP endpoint. Production provisioning must supply the exact identity-provider `sub`, the tenant claim mapping and the tenant ID. Normal API startup neither migrates nor grants access.

## Local authentication and run

Development also requires a signed JWT. The SDK can create a local token and configure its signing key using user secrets:

```powershell
dotnet user-jwts create --project src/BusinessSuite.Api --name local-owner --claim tenant_id=11111111-1111-1111-1111-111111111111 --audience https://localhost:7140
dotnet dev-certs https --trust
dotnet run --project src/BusinessSuite.Api --launch-profile https
```

Use the returned token as `Authorization: Bearer <token>`. Swagger is at `https://localhost:7140/swagger` in Development. It supports the **Authorize** bearer token control. Production uses `Authentication__Authority` (HTTPS OIDC issuer) and `Authentication__Audience`; both are required outside Development. Tokens need a nonempty GUID `tenant_id` and a stable `sub`. Roles in tokens are not treated as database memberships. There is no header-based development impersonation handler in the shipped API.

## First API workflow

```powershell
$headers = @{ Authorization = 'Bearer YOUR_TOKEN' }
$base = 'https://localhost:7140/api/v1'
$portfolio = Invoke-RestMethod -Method Post -Uri "$base/portfolios" -Headers $headers -ContentType 'application/json' -Body (@{
    displayName = 'My portfolio'; reportingCurrency = 'EUR'
} | ConvertTo-Json)
$entity = Invoke-RestMethod -Method Post -Uri "$base/legal-entities" -Headers $headers -ContentType 'application/json' -Body (@{
    portfolioId = $portfolio.data.id; registeredName = 'Example Company'
    countryCode = 'CH'; registrationNumber = 'EXAMPLE-123'
    functionalCurrency = 'CHF'; locale = 'de-CH'; fiscalYearStartMonth = 1
} | ConvertTo-Json)
Invoke-RestMethod -Method Post -Uri "$base/branches" -Headers $headers -ContentType 'application/json' -Body (@{
    legalEntityId = $entity.data.id; displayName = 'Zurich'
    timeZone = 'Europe/Zurich'; address = 'Example address'
} | ConvertTo-Json)
```

| Method | Route | Behavior |
| --- | --- | --- |
| POST / GET | `/api/v1/portfolios` | Create / list visible portfolios |
| GET | `/api/v1/portfolios/{id}` | Read authorized portfolio |
| POST / GET | `/api/v1/legal-entities` | Create / list entities; list requires `portfolioId` |
| GET | `/api/v1/legal-entities/{id}` | Read authorized entity |
| POST / GET | `/api/v1/branches` | Create / list branches; list requires `legalEntityId` |
| GET | `/api/v1/branches/{id}` | Read authorized branch |
| GET | `/api/v1/reference-data/currencies` | Supported initial currency catalog |
| GET | `/health/live` | Anonymous process liveness |
| GET | `/health/ready` | Anonymous database connectivity and pending migration status |

List routes accept `offset=0&limit=50`, with a maximum limit of 100. They filter by access **before** pagination and use stable ID ordering. Descendant grants do not expose parent details: a branch-only user can list their branches with a known `legalEntityId`, but cannot read the parent legal entity or sibling branches. Missing and unauthorized direct resources return 404. Authenticated callers without management permission receive 403 for creation; missing/invalid credentials receive 401. Invalid data returns 400, duplicate registered company identifiers return 409 on PostgreSQL, and throttling returns 429. Never blindly retry POST requests: general mutation idempotency is a later increment.

## Verify and deliver

```powershell
./scripts/verify.ps1
```

This restores the pinned EF tool, builds Release, runs tests, checks migration model drift and writes reviewed-deployment input to `.artifacts/migrations.sql`. The tests use relational SQLite with foreign keys and transactions; they do not certify PostgreSQL deployment. Apply migrations to a disposable PostgreSQL environment and smoke-test authenticated requests before deploying. No database or cloud deployment was performed as part of creating this foundation.

The GitHub Actions template needs repository variables `FRAMEWORK_REPOSITORY` (`owner/repo`) and `FRAMEWORK_REF` (reviewed commit/tag). For a private Framework repository, provide a read-only `FRAMEWORK_READ_TOKEN` secret. Configure these when BusinessSuite is placed in source control.

Build the container from the common repositories directory:

```powershell
docker build -f BusinessSuite/Dockerfile -t businesssuite-api:local .
```

The Dockerfile-specific ignore file includes only the necessary project sources, excluding build outputs and local secrets. The image runs as a non-root user on port 8080. Container tooling was not available during initial verification.

See [infrastructure contract](infrastructure/README.md) for deployment settings and operational requirements.

AI ownership: the independent AI-Framework sibling supplies AIFramework.Core; general Framework no longer contains AI libraries. The Docker build and CI checkout include the required Core sources. BusinessSuite still has no operational AI endpoints.
