using EhrBridge.Api.Data;
using EhrBridge.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EhrBridge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Base route: /api/audit
    public class AuditController : ControllerBase
    {
        private readonly ILogger<AuditController> _logger;
        private readonly AuditService _auditService;

        public AuditController(ILogger<AuditController> logger, AuditService auditService)
        {
            _logger = logger;
            _auditService = auditService;
        }

        /// <summary>
        /// Executes the patient data quality audit.
        /// Route: GET /api/audit
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<AuditResultDto>> GetAuditResults(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GET /api/audit endpoint called.");

            var result = await _auditService.RunDataQualityAuditAsync(stoppingToken);

            return Ok(result);
        }
    }
}
