using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Collections.Generic;

namespace EhrIntegrationBridge
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _connectionString;
        
        // Define the target count for the stress test demo
        private const int PATIENT_COUNT_TARGET = 1000; 

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;

            // Read connection string from environment in the order of precedence used in the compose file
            _connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__EhrDatabase") ??
                Environment.GetEnvironmentVariable("MYSQL_CONNECTION") ??
                "Server=mariadb;Database=openemr;User Id=openemr;Password=openemrpass;";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("--- EHR BRIDGE WORKER STARTING ---");
            _logger.LogInformation("      - Mode: Sync");
            _logger.LogInformation("🔗 Using connection string: {Conn}", _connectionString);

            try
            {
                // Ensure connection is established before proceeding
                await WaitForDatabaseAsync(stoppingToken); 
                
                // New step: Seed 1,000 test patients
                await SeedTestPatientsAsync(stoppingToken); 
            }
            catch (Exception ex)
            {
                // The worker failed before the final cleanup, we must re-run to fix data if needed.
                _logger.LogError(ex, "Unexpected error in worker.");
            }

            _logger.LogInformation("--- EHR BRIDGE WORKER FINISHED ---");
        }

        private async Task WaitForDatabaseAsync(CancellationToken token)
        {
            bool connected = false;
            // Explicitly check if connection string is not null or empty before attempting connection
            while (string.IsNullOrEmpty(_connectionString) == false && !connected && !token.IsCancellationRequested)
            {
                try
                {
                    await using var conn = new MySqlConnection(_connectionString);
                    await conn.OpenAsync(token);
                    connected = true;
                    _logger.LogInformation("✅ Database connection successful.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Database not ready yet: {Message}", ex.Message);
                    await Task.Delay(2000, token);
                }
            }
        }

        private async Task SeedTestPatientsAsync(CancellationToken token)
        {
            _logger.LogInformation($"--- Seeding local OpenEMR with {PATIENT_COUNT_TARGET} test patients ---");

            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync(token);

            // Verify patient_data table exists
            await using (var checkCmd = new MySqlCommand("SHOW TABLES LIKE 'patient_data';", conn))
            {
                var result = await checkCmd.ExecuteScalarAsync(token);
                if (result == null)
                {
                    _logger.LogError("❌ patient_data table not found.");
                    return;
                }

                _logger.LogInformation("✅ Detected patient table: patient_data");
            }
            
            // Dynamically query the maximum existing PID to prevent collisions
            int maxPid = 0;
            string maxPidSql = "SELECT MAX(pid) FROM patient_data;";
            
            await using (var maxCmd = new MySqlCommand(maxPidSql, conn))
            {
                var result = await maxCmd.ExecuteScalarAsync(token);
                // Handle DBNull if table is empty, or long if data exists
                if (result != DBNull.Value && result is long currentMaxLong) 
                {
                    maxPid = (int)currentMaxLong;
                }
            }
            
            // Start patient generation AFTER the highest existing PID
            int startPid = maxPid + 1; 

            _logger.LogInformation($"... Database's current MAX PID is {maxPid}. Starting new patients at PID {startPid}.");

            // 1. GENERATE PATIENTS using the Bogus Faker logic
            _logger.LogInformation($"... Generating {PATIENT_COUNT_TARGET} patient records (approx. 20% incomplete)...");
            // Pass the dynamically calculated startPid to the generator
            List<Patient> patientsToSeed = PatientDataGenerator.GeneratePatients(PATIENT_COUNT_TARGET, startPid);
            
            _logger.LogInformation($"... Starting batch insertion of {patientsToSeed.Count} records.");

            int insertedCount = 0;
            // 2. INSERT PATIENTS
            foreach (var patient in patientsToSeed)
            {
                if (token.IsCancellationRequested)
                {
                    _logger.LogWarning("Seeding cancelled mid-operation.");
                    break;
                }
                
                // Use a dedicated command creation method for clarity and SQL parameter safety
                await using (var insertCmd = CreatePatientRecordCommand(conn, patient))
                {
                    insertedCount += await insertCmd.ExecuteNonQueryAsync(token);
                }
            }
            
            _logger.LogInformation("Total inserted rows: {Count}", insertedCount);

            // 3. ENSURE PID IS SET (Safety check for records that might use the 'id' auto-increment column)
            string updatePidSql = "UPDATE patient_data SET pid = id WHERE pid = 0;";
            await using (var updateCmd = new MySqlCommand(updatePidSql, conn))
            {
                var updated = await updateCmd.ExecuteNonQueryAsync(token);
                _logger.LogInformation("Updated pid for {Count} records where pid was 0.", updated); 
            }

            _logger.LogInformation("✅ Seeding finished.");
        }
        
        private MySqlCommand CreatePatientRecordCommand(MySqlConnection conn, Patient patient)
        {
            // 💡 FIX: Added 'ss' (SSN) and 'date' (Record Creation Date) to the list of columns
            string insertSql = @"
INSERT INTO patient_data
(pid, uuid, title, language, financial, fname, lname, sex, DOB, street, city, state, postal_code, country_code, email, phone_home, ss, date)
VALUES
(@pid, UNHEX(REPLACE(@uuid, '-', '')), 'Mr', 'English', 'Self-Pay', @fname, @lname, @sex, @DOB, @street, @city, @state, @postal_code, 'US', @email, @phone, @ss, @date);";

            var insertCmd = new MySqlCommand(insertSql, conn);
            
            insertCmd.Parameters.AddWithValue("@pid", patient.Pid); 

            // Generate a unique, standards-compliant GUID for the UUID column
            var uuidString = Guid.NewGuid().ToString(); 
            
            insertCmd.Parameters.AddWithValue("@uuid", uuidString);
            insertCmd.Parameters.AddWithValue("@fname", patient.FirstName);
            insertCmd.Parameters.AddWithValue("@lname", patient.LastName);
            insertCmd.Parameters.AddWithValue("@sex", patient.Sex); 
            insertCmd.Parameters.AddWithValue("@DOB", patient.DateOfBirth.ToString("yyyy-MM-dd")); 
            insertCmd.Parameters.AddWithValue("@street", patient.StreetAddress);
            insertCmd.Parameters.AddWithValue("@city", patient.City);
            insertCmd.Parameters.AddWithValue("@state", patient.State);
            insertCmd.Parameters.AddWithValue("@postal_code", patient.PostalCode);
            
            string email = $"{patient.FirstName}.{patient.LastName}@{patient.City?.Replace(" ", "").ToLowerInvariant()}.com";
            insertCmd.Parameters.AddWithValue("@email", email); 
            
            // Phone home fix remains: use empty string if null
            insertCmd.Parameters.AddWithValue("@phone", patient.PhoneNumber ?? string.Empty); 
            
            // SSN fix is kept
            insertCmd.Parameters.AddWithValue("@ss", patient.SocialSecurityNumber);
            
            // 💡 NEW FIX: Add the current datetime for the 'date' column
            insertCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            return insertCmd;
        }
    }
}