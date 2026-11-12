using IKhethelo.Digital.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IKhethelo.Digital.Api.Controllers;

[ApiController, Authorize(Policy = "AdministratorManagement")]
[Route("api/v1/admin-users")]
public sealed class AdminUsersController(IAdminUserManagement users) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(AdminCreate request, CancellationToken ct)
    {
        var result = await users.CreateAsync(request, ct);
        var tenant = Request.Query["tenantCode"].ToString();
        return Created($"/api/v1/admin-users/{result.Id}" + (tenant.Length == 0 ? "" : "?tenantCode=" + Uri.EscapeDataString(tenant)), result);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default) => Ok(await users.ListAsync(new(page, pageSize, search, isActive), ct));
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) => Ok(await users.GetAsync(ResourceId.Parse(id), ct));
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, AdminUpdate request, CancellationToken ct) => Ok(await users.UpdateAsync(ResourceId.Parse(id), request, ct));
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> Status(string id, AdminStatusUpdate request, CancellationToken ct) => Ok(await users.StatusAsync(ResourceId.Parse(id), request, ct));
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> Reset(string id, AdminPasswordReset request, CancellationToken ct)
    {
        await users.ResetPasswordAsync(ResourceId.Parse(id), request, ct); return NoContent();
    }
}

[ApiController, Authorize(Policy = "MembershipAccess")]
[Route("api/v1/admin/tenants/{tenantCode}")]
public sealed class MemberOperationsController(IMemberOperations operations) : ControllerBase
{
    [HttpPost("members")]
    public async Task<IActionResult> Register(MemberRegistration request, CancellationToken ct)
    {
        var result = await operations.RegisterAsync(request, ct);
        return Created($"/api/v1/admin/tenants/{Uri.EscapeDataString((string)RouteData.Values["tenantCode"]!)}/members/{result.MemberId}", result);
    }
    [HttpGet("members/{memberId}/category")]
    public async Task<IActionResult> Category(string memberId, CancellationToken ct) => Ok(await operations.CategoryAsync(ResourceId.Parse(memberId), ct));
    [HttpPut("members/{memberId}/category")]
    public async Task<IActionResult> Assign(string memberId, MemberCategoryUpdate request, CancellationToken ct) => Ok(await operations.AssignCategoryAsync(ResourceId.Parse(memberId), request, ct));
    [HttpGet("members/{memberId}/history")]
    public async Task<IActionResult> History(string memberId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await operations.HistoryAsync(ResourceId.Parse(memberId), new(page, pageSize), ct));
    [HttpGet("membership-categories")]
    public async Task<IActionResult> Categories([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string? status = null, CancellationToken ct = default) => Ok(await operations.CategoriesAsync(new(page, pageSize, search, status), ct));
    [HttpPost("membership-categories")]
    public async Task<IActionResult> CreateCategory(CategoryWrite request, CancellationToken ct)
    {
        var result = await operations.CreateCategoryAsync(request, ct); return StatusCode(201, result);
    }
    [HttpPut("membership-categories/{id}")]
    public async Task<IActionResult> UpdateCategory(string id, CategoryWrite request, CancellationToken ct) => Ok(await operations.UpdateCategoryAsync(ResourceId.Parse(id), request, ct));
}
