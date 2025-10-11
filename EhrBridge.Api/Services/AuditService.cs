using EhrBridge.Api.Data;
using MySqlConnector;

namespace EhrBridge.Api.Services;

public class AuditService
{
    private readonly ILogger<AuditService> _logger;
    // The mariadb connection string, using the Docker service name 'mariadb' and port '3000'
    private const string ConnectionString = "server=mariadb;port=3000;database=openemr;user=openemr;password=openemrpass;SslMode=None;";

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    // Public method to execute the core data quality audit.
    public async Task<AuditResultDto> RunDataQualityAuditAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Data Quality Audit on local OpenEMR database...");
        
        var result = new AuditResultDto();
        var allPatients = 0;
        
        try
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync(stoppingToken);
            
            // Core Audit Query: Select patient records where the Patient ID is valid.
            // In the original Worker Service, this query was WHERE pid > 0
            var command = new MySqlCommand("SELECT pid, fname, lname, phone_cell, street FROM patient_data WHERE pid > 0;", connection);
            await using var reader = await command.ExecuteReaderAsync(stoppingToken);

            while (await reader.ReadAsync(stoppingToken))
            {
                allPatients++;
                var patientId = reader.GetInt64("pid");
                var firstName = reader.GetString("fname");
                var lastName = reader.GetString("lname");
                // Note: GetString will return string.Empty for NULL database fields, which is perfect for our audit logic.
                var phone = reader.GetString("phone_cell");
                var street = reader.GetString("street");

                // Audit Rule: Check for missing street OR missing phone
                if (string.IsNullOrEmpty(street) || string.IsNullOrEmpty(phone))
                {
                    result.IncompleteRecordsFound++;
                    
                    // Populate the DTO with minimal PHI required for administrative action
                    result.IncompleteRecords.Add(new IncompleteRecordDto
                    {
                        PatientId = patientId,
                        FirstName = firstName,
                        LastName = lastName,
                        MissingDataFlag = string.IsNullOrEmpty(street) ? "Missing Address (street)" : "Missing Phone (phone_cell)"
                    });
                }
            }
            
            result.TotalRecordsScanned = allPatients;
            _logger.LogInformation("Analysis complete. Found {Count} incomplete records.", result.IncompleteRecordsFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during the data quality audit in the AuditService. Returning empty result set.");
            // Return empty results on failure to prevent crashing the API
            return new AuditResultDto { TotalRecordsScanned = 0, IncompleteRecordsFound = 0 };
        }
        
        return result;
    }
}
