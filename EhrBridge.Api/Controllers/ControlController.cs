using EhrBridge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EhrBridge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ControlController : ControllerBase
{
    private readonly IControlService _controlService;

    public ControlController(IControlService controlService)
    {
        _controlService = controlService;
    }

    /// <summary>
    /// POST: /api/control/reseed - Triggers the database cleanup and reseeding of 1,000 patient records.
    /// Audit is intentionally NOT called here to decouple seeding from auditing.
    /// </summary>
    [HttpPost("reseed")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReSeedData()
    {
        try
        {
            await _controlService.ReSeedDemoDataAsync();

            return Accepted(new
            {
                message = "Database re-seed successfully initiated. Audit is decoupled and must be run separately."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"Failed to re-seed data: {ex.Message}");
        }
    }
}
