using System.Data;
using MySql.Data.MySqlClient;
using EhrBridge.Api.Data;
using EhrBridge.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EhrBridge.Api.Services
{
    public class AuditService : IAuditService
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

            cancellationToken.ThrowIfCancellationRequested();

            var incompleteRecords = await GetIncompleteDemographicsAsync();
            var totalCount = await GetTotalPatientCountAsync();

            var result = new AuditResultDto
            {
                TotalRecordsScanned = totalCount,
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
            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt32(result ?? 0);
        }

        private async Task<List<IncompleteRecord>> GetIncompleteDemographicsAsync()
        {
            var incompleteRecords = new List<IncompleteRecord>();

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // ✅ use correct column name 'phone_cell' instead of 'phone_home'
            var query = @"
                SELECT pid, fname, lname, phone_cell, street 
                FROM patient_data;
            ";

            await using var command = new MySqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var pid = reader.GetInt32("pid");
                var fname = reader["fname"]?.ToString();
                var lname = reader["lname"]?.ToString();
                var phone = reader["phone_cell"]?.ToString();
                var street = reader["street"]?.ToString();

                var missingFields = new List<string>();

                if (string.IsNullOrWhiteSpace(fname)) missingFields.Add("First Name");
                if (string.IsNullOrWhiteSpace(lname)) missingFields.Add("Last Name");
                if (string.IsNullOrWhiteSpace(street)) missingFields.Add("Address");
                if (string.IsNullOrWhiteSpace(phone)) missingFields.Add("Phone (Cell)");

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
