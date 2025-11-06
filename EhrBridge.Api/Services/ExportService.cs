// /workspaces/EHR-Integration-Bridge/EhrBridge.Api/Services/ExportService.cs
using System.Data;
using MySql.Data.MySqlClient;
using EhrBridge.Api.Data;
using EhrBridge.Api.DataGeneration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EhrBridge.Api.Services
{
    public class ExportService : IExportService
    {
        private readonly string _connectionString;
        private readonly ILogger<ExportService> _logger;

        public ExportService(IConfiguration config, ILogger<ExportService> logger)
        {
            // Use the same configuration retrieval pattern as AuditService
            _connectionString = config.GetConnectionString("EhrDatabase")
                // Use explicit logic for safety check
                ?? (string.IsNullOrEmpty(config.GetConnectionString("EhrDatabase")) == true ? throw new ArgumentNullException(nameof(_connectionString)) : throw new InvalidOperationException("EhrDatabase connection string is null or empty."));
            _logger = logger;
        }

        public async IAsyncEnumerable<CleanPatientRecord> StreamAllPatientRecordsAsync()
        {
            // WARNING: PHI Exposure Risk mitigated by streaming.
            _logger.LogInformation("Streaming all patient records for full export (CleanPatientRecord DTO).");

            // SQL to retrieve all fields required for the CleanPatientRecord DTO
            // 🛑 FINAL FIX: Left-align the SQL content to eliminate leading whitespace from the verbatim string.
            const string sql = @"SELECT 
pid AS PatientId, 
fname AS FirstName, 
lname AS LastName, 
dob AS DateOfBirth, 
street AS StreetAddress, 
phone_cell AS PhoneNumber, 
postal_code AS PostalCode,
city AS City,
state AS State,
sex AS Gender
FROM patient_data 
WHERE pid > 0;";
            
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);
            
            // CommandBehavior.CloseConnection ensures the connection is closed after the reader finishes/disposes.
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);

            while (await reader.ReadAsync())
            {
                yield return new CleanPatientRecord
                {
                    PatientId = reader.GetInt64("PatientId"),
                    FirstName = reader.GetString("FirstName"),
                    LastName = reader.GetString("LastName"),
                    DateOfBirth = reader.IsDBNull("DateOfBirth") == false ? reader.GetDateTime("DateOfBirth").ToString("yyyy-MM-dd") : string.Empty,
                    StreetAddress = reader.IsDBNull("StreetAddress") == false ? reader.GetString("StreetAddress") : string.Empty,
                    PhoneNumber = reader.IsDBNull("PhoneNumber") == false ? reader.GetString("PhoneNumber") : string.Empty,
                    PostalCode = reader.IsDBNull("PostalCode") == false ? reader.GetString("PostalCode") : string.Empty,
                    City = reader.IsDBNull("City") == false ? reader.GetString("City") : string.Empty,
                    State = reader.IsDBNull("State") == false ? reader.GetString("State") : string.Empty,
                    Gender = reader.IsDBNull("Gender") == false ? reader.GetString("Gender") : string.Empty,
                };
            }
        }

        public async IAsyncEnumerable<IncompleteRecordDto> StreamIncompleteDemographicRecordsAsync()
        {
            _logger.LogInformation("Streaming incomplete demographic records for audit export (IncompleteRecordDto).");

            // SQL re-uses the core logic of the AuditService (missing street OR missing phone_cell).
            // 🛑 FINAL FIX: Left-align the SQL content to eliminate leading whitespace from the verbatim string.
            const string sql = @"SELECT 
pid AS PatientId,
fname AS FirstName, 
lname AS LastName,
street,
phone_cell
FROM patient_data 
WHERE pid > 0 
AND (street IS NULL OR street = '' OR phone_cell IS NULL OR phone_cell = '');";

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new MySqlCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection);

            while (await reader.ReadAsync())
            {
                // Determine the missing fields to dynamically populate the descriptive fields
                var streetMissing = reader.IsDBNull("street") || string.IsNullOrEmpty(reader.GetString("street"));
                var phoneMissing = reader.IsDBNull("phone_cell") || string.IsNullOrEmpty(reader.GetString("phone_cell"));
                var missingField = (streetMissing && phoneMissing) ? "Address and Phone" : (streetMissing ? "Street Address" : "Phone Number");
                var description = $"Missing required field(s): {missingField}.";

                yield return new IncompleteRecordDto
                {
                    PatientId = reader.GetInt32("PatientId"),
                    FirstName = reader.GetString("FirstName"),
                    LastName = reader.GetString("LastName"),
                    Field = missingField, 
                    Description = description,
                    Reason = "Incomplete demographic record for export."
                };
            }
        }
    }
}