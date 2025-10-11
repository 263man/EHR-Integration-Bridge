using EhrBridge.Api.Data;
using EhrBridge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EhrBridge.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // This establishes the base route as /api/Audit
public class AuditController : ControllerBase
{
    private readonly ILogger<AuditController> _logger;
    private readonly AuditService _auditService;

    // Constructor Injection: The AuditService is provided by the DI container.
    public AuditController(ILogger<AuditController> logger, AuditService auditService)
    {
        _logger = logger;
        _auditService = auditService;
    }

    /// <summary>
    /// Executes the patient data quality audit against the EHR database.
    /// Route: GET /api/audit
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditResultDto>> GetAuditResults(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GET /api/audit endpoint called.");
        
        // Call the core business logic from the service layer
        var result = await _auditService.RunDataQualityAuditAsync(stoppingToken);

        // Return the structured DTO result as a 200 OK
        return Ok(result);
    }
}
