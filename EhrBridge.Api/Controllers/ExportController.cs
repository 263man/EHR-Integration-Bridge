// EhrBridge.Api/Controllers/ExportController.cs
using EhrBridge.Api.Services;
using EhrBridge.Api.Utils; // Use the new utility classes
using Microsoft.AspNetCore.Mvc;

namespace EhrBridge.Api.Controllers
{
    [ApiController]
    [Route("export")]
    public class ExportController : ControllerBase
    {
        private readonly IExportService _exportService;
        private readonly ILogger<ExportController> _logger;

        public ExportController(IExportService exportService, ILogger<ExportController> logger)
        {
            _exportService = exportService;
            _logger = logger;
        }

        /// <summary>
        /// GET /export/all - Downloads a CSV file containing ALL patient records.
        /// Streams the data directly from the database to the HTTP response (non-buffering).
        /// </summary>
        [HttpGet("all")]
        [Produces("text/csv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ExportAllRecordsCsv()
        {
            _logger.LogInformation("Request received for GET /export/all.");
            
            try
            {
                var records = _exportService.StreamAllPatientRecordsAsync();
                
                // Set necessary headers for file download
                Response.Headers.Add("Content-Disposition", "attachment; filename=\"Full_Patient_Export.csv\"");
                Response.ContentType = "text/csv";

                // Return custom result to stream data directly
                return new FileCallbackResult("text/csv", async (outputStream, _) =>
                {
                    // Stream the data from the DB to the HTTP response stream
                    await CsvExportUtility.WriteCsvToStreamAsync(records, outputStream);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate full patient export CSV.");
                // Return generic 500 error, details logged server-side (HIPAA/PHI best practice)
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate full patient export CSV. Please check API logs.");
            }
        }

        /// <summary>
        /// GET /export/incomplete - Downloads a CSV file containing only incomplete demographic records.
        /// Streams the data directly from the database to the HTTP response (non-buffering).
        /// </summary>
        [HttpGet("incomplete")]
        [Produces("text/csv")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult ExportIncompleteRecordsCsv()
        {
            _logger.LogInformation("Request received for GET /export/incomplete.");

            try
            {
                var records = _exportService.StreamIncompleteDemographicRecordsAsync();

                // Set necessary headers for file download
                Response.Headers.Add("Content-Disposition", "attachment; filename=\"Incomplete_Demographics_Audit_List.csv\"");
                Response.ContentType = "text/csv";

                // Return custom result to stream data directly
                return new FileCallbackResult("text/csv", async (outputStream, _) =>
                {
                    // Stream the data from the DB to the HTTP response stream
                    await CsvExportUtility.WriteCsvToStreamAsync(records, outputStream);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate incomplete records export CSV.");
                // Return generic 500 error
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate incomplete records export CSV. Please check API logs.");
            }
        }
    }
}