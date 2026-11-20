using IKhethelo.Digital.Application;
using IKhethelo.Digital.Api.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IKhethelo.Digital.Api.Controllers;

[ApiController]
[Route("api/v1/public/{tenantCode}/membership-applications")]
public sealed class SubmissionController(ISubmitMembershipApplication submit) : ControllerBase
{
    [HttpPost, AllowAnonymous, EnableRateLimiting("PublicSubmission")]
    public async Task<IActionResult> Submit(ApplicationSubmission request, CancellationToken ct) => StatusCode(201, await submit.ExecuteAsync(request, ct));
}

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthenticationController(IAdministratorAuthentication authentication, IChangeOwnPassword changePassword) : ControllerBase
{
    [HttpGet("password-policy"), Authorize]
    public IActionResult PasswordPolicy() => Ok(new { guidance = AdministratorValidation.PasswordGuidance });
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("Login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var token = await authentication.ExecuteAsync(request, ct);
        return token is null ? StatusCode(401, Problems.Create(HttpContext, 401, "Invalid credentials.")) : Ok(token);
    }
    [HttpPost("change-password"), Authorize, EnableRateLimiting("Login")]
    public async Task<IActionResult> ChangePassword(ChangeOwnPassword request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var actor) ||
            !int.TryParse(User.FindFirst("security_version")?.Value, out var version)) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        await changePassword.ExecuteAsync(actor, version, request, ct);
        return NoContent();
    }
}

[ApiController, Authorize(Policy = "MembershipAccess")]
[Route("api/v1/admin/tenants/{tenantCode}/membership-applications")]
public sealed class ApplicationsController(IMembershipQueries queries, IReviewApplications review) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string? status = null, CancellationToken ct = default) => Ok(await queries.ApplicationsAsync(new(page, pageSize, search, status), ct));
    [HttpGet("{applicationId}")]
    public async Task<IActionResult> Get(string applicationId, CancellationToken ct) => Ok(await queries.ApplicationAsync(ResourceId.Parse(applicationId), ct));
    [HttpPost("{applicationId}/activate")]
    public async Task<IActionResult> Activate(string applicationId, ReviewRequest request, CancellationToken ct) => Ok(await review.ActivateAsync(ResourceId.Parse(applicationId), request, ct));
    [HttpPost("{applicationId}/approve")]
    public async Task<IActionResult> Approve(string applicationId, ReviewRequest request, CancellationToken ct) => Ok(await review.ApproveAsync(ResourceId.Parse(applicationId), request, ct));
    [HttpPost("{applicationId}/decline")]
    public async Task<IActionResult> Decline(string applicationId, ReviewRequest request, CancellationToken ct) => Ok(await review.DeclineAsync(ResourceId.Parse(applicationId), request, ct));
}

[ApiController, Authorize(Policy = "MembershipAccess")]
[Route("api/v1/admin/tenants/{tenantCode}/members")]
public sealed class MembersController(IMembershipQueries queries, IMemberAdministration administration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string? status = null, CancellationToken ct = default) => Ok(await queries.MembersAsync(new(page, pageSize, search, status), ct));
    [HttpGet("{memberId}")]
    public async Task<IActionResult> Get(string memberId, CancellationToken ct) => Ok(await queries.MemberAsync(ResourceId.Parse(memberId), ct));
    [HttpPut("{memberId}")]
    public async Task<IActionResult> Update(string memberId, MemberUpdate request, CancellationToken ct) => Ok(await administration.UpdateAsync(ResourceId.Parse(memberId), request, ct));
    [HttpPut("{memberId}/status")]
    public async Task<IActionResult> Status(string memberId, MemberStatusUpdate request, CancellationToken ct) => Ok(await administration.StatusAsync(ResourceId.Parse(memberId), request, ct));
}

internal static class ResourceId
{
    public static Guid Parse(string value) => Guid.TryParse(value, out var id) ? id : throw new RequestFailure(400, "Invalid resource identifier.");
}
