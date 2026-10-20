using BusinessSuite.Application.Portfolio;
using BusinessSuite.Contracts.Portfolio;
using Framework.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BusinessSuite.Api.Controllers;

[ApiController, Authorize, Route("api/v1/portfolios")]
public sealed class PortfoliosController(PortfolioService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreatePortfolioRequest request, CancellationToken ct)
    {
        var result = await service.CreatePortfolio(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, CommonResult<PortfolioResponse>.Success(result));
    }
    [HttpGet("{id:guid}")]
    public async Task<CommonResult<PortfolioResponse>> Get(Guid id, CancellationToken ct) => CommonResult<PortfolioResponse>.Success(await service.GetPortfolio(id, ct));
    [HttpGet]
    public async Task<CommonResult<IReadOnlyList<PortfolioResponse>>> List(CancellationToken ct, [FromQuery] int offset = 0, [FromQuery] int limit = 50) =>
        CommonResult<IReadOnlyList<PortfolioResponse>>.Success(await service.ListPortfolios(offset, limit, ct));
}

[ApiController, Authorize, Route("api/v1/legal-entities")]
public sealed class LegalEntitiesController(PortfolioService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateLegalEntityRequest request, CancellationToken ct)
    {
        var result = await service.CreateLegalEntity(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, CommonResult<LegalEntityResponse>.Success(result));
    }
    [HttpGet("{id:guid}")]
    public async Task<CommonResult<LegalEntityResponse>> Get(Guid id, CancellationToken ct) => CommonResult<LegalEntityResponse>.Success(await service.GetLegalEntity(id, ct));
    [HttpGet]
    public async Task<CommonResult<IReadOnlyList<LegalEntityResponse>>> List([FromQuery] Guid portfolioId, CancellationToken ct, [FromQuery] int offset = 0, [FromQuery] int limit = 50) =>
        CommonResult<IReadOnlyList<LegalEntityResponse>>.Success(await service.ListLegalEntities(portfolioId, offset, limit, ct));
}

[ApiController, Authorize, Route("api/v1/branches")]
public sealed class BranchesController(PortfolioService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateBranchRequest request, CancellationToken ct)
    {
        var result = await service.CreateBranch(request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, CommonResult<BranchResponse>.Success(result));
    }
    [HttpGet("{id:guid}")]
    public async Task<CommonResult<BranchResponse>> Get(Guid id, CancellationToken ct) => CommonResult<BranchResponse>.Success(await service.GetBranch(id, ct));
    [HttpGet]
    public async Task<CommonResult<IReadOnlyList<BranchResponse>>> List([FromQuery] Guid legalEntityId, CancellationToken ct, [FromQuery] int offset = 0, [FromQuery] int limit = 50) =>
        CommonResult<IReadOnlyList<BranchResponse>>.Success(await service.ListBranches(legalEntityId, offset, limit, ct));
}
