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
        // 🛑 FIX: Use the IAuditService interface here
        private readonly IAuditService _auditService;

        // 🛑 FIX: Use the IAuditService interface in the constructor argument
        public AuditController(ILogger<AuditController> logger, IAuditService auditService) 
        {
            _logger = logger;
            _auditService = auditService;
        }

        /// <summary>
        /// Executes the patient data quality audit and returns the latest results.
        /// Route: GET /api/audit
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuditResultDto))]
        public async Task<ActionResult<AuditResultDto>> GetAuditResults(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GET /api/audit endpoint called.");

            var result = await _auditService.RunDataQualityAuditAsync(stoppingToken);

            return Ok(result);
        }
        
        // This endpoint returns only the list of records that failed the demographic audit.
        /// <summary>
        /// GET /api/Audit/incomplete-demographics - Retrieves the list of records that failed the audit.
        /// </summary>
        [HttpGet("incomplete-demographics")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<IncompleteRecordDto>))]
        public async Task<ActionResult<IEnumerable<IncompleteRecordDto>>> GetIncompleteDemographics(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GET /api/Audit/incomplete-demographics endpoint called.");

            // Since the main audit method returns the full result, extract the incomplete list.
            var fullResult = await _auditService.RunDataQualityAuditAsync(stoppingToken);

            return Ok(fullResult.IncompleteRecords);
        }
    }
}