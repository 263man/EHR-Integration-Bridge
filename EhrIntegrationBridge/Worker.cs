using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace EhrIntegrationBridge
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _connectionString;

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
            _logger.LogInformation("         - Mode: Sync");
            _logger.LogInformation("🔗 Using connection string: {Conn}", _connectionString);

            try
            {
                await WaitForDatabaseAsync(stoppingToken);
                await SeedTestPatientsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker.");
            }

            _logger.LogInformation("--- EHR BRIDGE WORKER FINISHED ---");
        }

        private async Task WaitForDatabaseAsync(CancellationToken token)
        {
            bool connected = false;
            while (!connected && !token.IsCancellationRequested)
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
            _logger.LogInformation("--- Seeding local OpenEMR with new test patients ---");

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

            // Generate a .NET GUID string and let MySQL convert it to binary(16) using UNHEX(REPLACE(...))
            var uuidString = Guid.NewGuid().ToString();

            // We use UNHEX(REPLACE(@uuid, '-', '')) so the column (binary(16)) gets the correct 16 bytes.
            string insertSql = @"
INSERT INTO patient_data
(uuid, title, language, financial, fname, lname, mname, DOB, street, city, state, country_code, email)
VALUES
(UNHEX(REPLACE(@uuid, '-', '')), 'Mr', 'English', 'Self-Pay', 'John', 'Doe', '', '1980-01-01', '123 Main St', 'Cityville', 'CA', 'US', 'john.doe@example.com');";

            await using (var insertCmd = new MySqlCommand(insertSql, conn))
            {
                insertCmd.Parameters.AddWithValue("@uuid", uuidString);
                var rows = await insertCmd.ExecuteNonQueryAsync(token);
                _logger.LogInformation("Inserted rows: {Count}", rows);
            }

            // Ensure pid is set (some OpenEMR installs keep pid = 0 until manually updated)
            string updatePidSql = "UPDATE patient_data SET pid = id WHERE pid = 0;";
            await using (var updateCmd = new MySqlCommand(updatePidSql, conn))
            {
                var updated = await updateCmd.ExecuteNonQueryAsync(token);
                _logger.LogInformation("Updated pid for {Count} records where pid was 0.", updated);
            }

            _logger.LogInformation("✅ Seeding finished.");
        }
    }
}
