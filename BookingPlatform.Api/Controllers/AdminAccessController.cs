using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.AdminAccess;
using BookingPlatform.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/admin-access")]
public sealed class AdminAccessController : ControllerBase
{
    private readonly AdminAccessService _adminAccessService;

    public AdminAccessController(AdminAccessService adminAccessService)
    {
        _adminAccessService = adminAccessService;
    }

    [HttpPost("request-code")]
    [ProducesResponseType(typeof(RequestAdminAccessCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestAdminAccessCodeResponse>> RequestCode(
        [FromBody] RequestAdminAccessCodeRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _adminAccessService.RequestCodeAsync(
            request.Email,
            ipAddress,
            cancellationToken);

        if (!result.Succeeded)
            return BadRequest(new ApiErrorResponse
            {
                Message = result.Message,
                ReasonCode = "admin_access_denied",
                ReasonCodes = new List<string> { "admin_access_denied" }
            });

        return Ok(result);
    }

    [HttpPost("verify-code")]
    [ProducesResponseType(typeof(VerifyAdminAccessCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VerifyAdminAccessCodeResponse>> VerifyCode(
        [FromBody] VerifyAdminAccessCodeRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _adminAccessService.VerifyCodeAsync(
            request.Email,
            request.Code,
            ipAddress,
            cancellationToken);

        if (!result.IsAllowed)
            return BadRequest(new ApiErrorResponse
            {
                Message = result.Message,
                ReasonCode = "invalid_admin_access_code",
                ReasonCodes = new List<string> { "invalid_admin_access_code" }
            });

        return Ok(result);
    }
}