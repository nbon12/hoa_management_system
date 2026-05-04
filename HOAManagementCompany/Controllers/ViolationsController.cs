using System.Security.Claims;
using HOAManagementCompany.Models;
using HOAManagementCompany.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOAManagementCompany.Controllers;

[ApiController]
[Route("api/violations")]
[Authorize]
public class ViolationsController : ControllerBase
{
    private readonly ViolationService _violationService;

    public ViolationsController(ViolationService violationService)
    {
        _violationService = violationService;
    }

    /// <summary>
    /// GET /api/violations/mine — paginated open violations for the current user's properties only.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(MyViolationsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMine([FromQuery] int limit = 10, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var (items, totalCount) = await _violationService.GetOpenViolationsForUserAsync(userId, limit, offset);
            return Ok(new MyViolationsResponseDto { Items = items, TotalCount = totalCount });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to load violations" });
        }
    }
}
