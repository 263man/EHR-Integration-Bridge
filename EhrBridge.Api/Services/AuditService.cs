using System.Data;
using MySql.Data.MySqlClient;
using EhrBridge.Api.Data;
using EhrBridge.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EhrBridge.Api.Services
{
    public class AuditService
    {
        private readonly string _connectionString;
        private readonly ILogger<AuditService> _logger;

        public AuditService(IConfiguration config, ILogger<AuditService> logger)
        {
            _connectionString = config.GetConnectionString("EhrDatabase")
                ?? throw new ArgumentNullException(nameof(_connectionString));
            _logger = logger;
        }

        public async Task<AuditResultDto> RunDataQualityAuditAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Data Quality Audit...");

            // Ensure the cancellation token is respected
            cancellationToken.ThrowIfCancellationRequested();

            var incompleteRecords = await GetIncompleteDemographicsAsync();

            var result = new AuditResultDto
            {
                TotalRecordsScanned = await GetTotalPatientCountAsync(),
                IncompleteRecordsFound = incompleteRecords.Count,
                IncompleteRecords = incompleteRecords.Select(r => new IncompleteRecordDto
                {
                    PatientId = r.PatientId,
                    Field = r.Field,
                    Description = r.Description
                }).ToList()
            };

            _logger.LogInformation("Audit complete. Found {count} incomplete records.", incompleteRecords.Count);

            return result;
        }

        private async Task<int> GetTotalPatientCountAsync()
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand("SELECT COUNT(*) FROM patient_data;", connection);
            // Using ExecuteScalarAsync to get the count
            var result = await command.ExecuteScalarAsync();
            
            // Explicitly handle DBNull.Value which can happen if the table is truly empty, though unlikely here.
            if (result == DBNull.Value || result is null)
            {
                return 0;
            }
            
            // Cast or convert to Int32
            return Convert.ToInt32(result);
        }

        private async Task<List<IncompleteRecord>> GetIncompleteDemographicsAsync()
        {
            var incompleteRecords = new List<IncompleteRecord>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // FIX: Changed 'phone_cell' to the correct column 'phone_home'
            var query = @"
                SELECT pid, fname, lname, phone_home, street 
                FROM patient_data;
            ";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var pid = reader.GetInt32("pid");
                var fname = reader["fname"]?.ToString() ?? "";
                var lname = reader["lname"]?.ToString() ?? "";
                
                // FIX: Retrieve the correct column name 'phone_home'
                var phone = reader["phone_home"]?.ToString() ?? ""; 
                var street = reader["street"]?.ToString() ?? "";

                var missingFields = new List<string>();

                // Use explicit check: string.IsNullOrWhiteSpace(x) == true
                if (string.IsNullOrWhiteSpace(fname) == true) missingFields.Add("First Name");
                if (string.IsNullOrWhiteSpace(lname) == true) missingFields.Add("Last Name");
                
                // This is the primary point of failure before the fix.
                // It now correctly checks the 'phone_home' column value.
                if (string.IsNullOrWhiteSpace(phone) == true) missingFields.Add("Phone (Home)");
                
                if (string.IsNullOrWhiteSpace(street) == true) missingFields.Add("Address");

                if (missingFields.Count > 0)
                {
                    incompleteRecords.Add(new IncompleteRecord
                    {
                        PatientId = pid,
                        Field = string.Join(", ", missingFields),
                        Description = "Missing required demographic fields."
                    });
                }
            }

            return incompleteRecords;
        }
    }
}