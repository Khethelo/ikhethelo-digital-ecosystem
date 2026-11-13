using IKhethelo.Digital.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IKhethelo.Digital.Api.Controllers;

[ApiController, Authorize(Policy = "NtmAccess")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/admin/tenants/{tenantCode}/ntm")]
public sealed class NtmController(INtmOperations operations) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(await operations.DashboardAsync(ct));
    [HttpGet("veterans")]
    public async Task<IActionResult> Veterans([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? hasBusiness = null, CancellationToken ct = default) =>
        Ok(await operations.VeteransAsync(new(page, pageSize, search, HasBusiness: hasBusiness), ct));
    [HttpGet("veterans/{id}")]
    public async Task<IActionResult> Veteran(string id, CancellationToken ct) => Ok(await operations.VeteranAsync(ResourceId.Parse(id), ct));
    [HttpPost("veterans")]
    public async Task<IActionResult> CreateVeteran(NtmVeteranWrite request, CancellationToken ct)
    {
        var result = await operations.CreateVeteranAsync(request, ct); return Created(ResourceLocation("veterans", result.Id), result);
    }
    [HttpPut("veterans/{id}")]
    public async Task<IActionResult> UpdateVeteran(string id, NtmVeteranWrite request, CancellationToken ct) =>
        Ok(await operations.UpdateVeteranAsync(ResourceId.Parse(id), request, ct));
    [HttpGet("businesses")]
    public async Task<IActionResult> Businesses([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? sector = null, [FromQuery] string? operationalStatus = null,
        [FromQuery] Guid? veteranId = null, CancellationToken ct = default) =>
        Ok(await operations.BusinessesAsync(new(page, pageSize, search, sector, operationalStatus, veteranId), ct));
    [HttpGet("businesses/{id}")]
    public async Task<IActionResult> Business(string id, CancellationToken ct) => Ok(await operations.BusinessAsync(ResourceId.Parse(id), ct));
    [HttpPost("businesses")]
    public async Task<IActionResult> CreateBusiness(NtmBusinessWrite request, CancellationToken ct)
    {
        var result = await operations.CreateBusinessAsync(request, ct); return Created(ResourceLocation("businesses", result.Id), result);
    }
    [HttpPut("businesses/{id}")]
    public async Task<IActionResult> UpdateBusiness(string id, NtmBusinessWrite request, CancellationToken ct) =>
        Ok(await operations.UpdateBusinessAsync(ResourceId.Parse(id), request, ct));
    private string ResourceLocation(string resource, Guid id) => $"/api/v1/admin/tenants/{Uri.EscapeDataString((string)RouteData.Values["tenantCode"]!)}/ntm/{resource}/{id}";
}
