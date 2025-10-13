using CsvHelper;
using MySqlConnector;
using System.Globalization;

namespace EhrIntegrationBridge;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerMode = Environment.GetEnvironmentVariable("WORKER_MODE") ?? "Extract";
        logger.LogInformation("--- EHR BRIDGE WORKER STARTING ---");
        logger.LogInformation("   - Mode: {Mode}", workerMode);

        if (!await WaitForDatabaseAsync(stoppingToken))
        {
            logger.LogCritical("Local database did not become available. Shutting down.");
            return;
        }

        if (workerMode.Equals("Sync", StringComparison.OrdinalIgnoreCase))
            await RunSyncAndSeedLogicAsync(stoppingToken);
        else
            await RunDataQualityAuditAsync(stoppingToken);

        logger.LogInformation("--- EHR BRIDGE WORKER FINISHED ---");
    }

    private async System.Threading.Tasks.Task<string> DetectPatientTableAsync(MySqlConnection connection)
    {
        var possibleTables = new[] { "patient_data", "patients", "demographics" };
        foreach (var table in possibleTables)
        {
            try
            {
                var checkCmd = new MySqlCommand($"SHOW TABLES LIKE '{table}';", connection);
                var result = await checkCmd.ExecuteScalarAsync();
                if (result != null)
                {
                    return table;
                }
            }
            catch
            {
                // ignore and try next
            }
        }

        throw new InvalidOperationException("❌ Could not detect patient table in OpenEMR database.");
    }

    private async System.Threading.Tasks.Task RunDataQualityAuditAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- Starting Data Quality Audit on local OpenEMR database ---");

        var allPatients = new List<CleanPatientRecord>();
        var incompleteRecords = new List<IncompleteDemographicRecord>();

        try
        {
            var connectionString = "server=mariadb;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);

            var patientTable = await DetectPatientTableAsync(connection);
            logger.LogInformation("✅ Detected patient table: {Table}", patientTable);

            var query = $@"SELECT pid, fname, lname, DOB, sex, phone_cell, street 
                           FROM {patientTable} WHERE pid > 0;";
            var command = new MySqlCommand(query, connection);

            await using var reader = await command.ExecuteReaderAsync(stoppingToken);
            while (await reader.ReadAsync(stoppingToken))
            {
                var patientId = reader.GetInt64("pid");
                var firstName = reader["fname"]?.ToString() ?? "";
                var lastName = reader["lname"]?.ToString() ?? "";
                var dob = reader["DOB"]?.ToString() ?? "";
                var gender = reader["sex"]?.ToString() ?? "";
                var phone = reader["phone_cell"]?.ToString() ?? "";
                var street = reader["street"]?.ToString() ?? "";

                allPatients.Add(new CleanPatientRecord
                {
                    PatientId = patientId,
                    FirstName = firstName,
                    LastName = lastName,
                    DateOfBirth = dob,
                    Gender = gender,
                    PhoneNumber = phone
                });

                if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(phone))
                {
                    incompleteRecords.Add(new IncompleteDemographicRecord
                    {
                        PatientId = patientId,
                        FirstName = firstName,
                        LastName = lastName,
                        MissingDataFlag = string.IsNullOrWhiteSpace(street) ? "Missing Address" : "Missing Phone"
                    });
                }
            }

            logger.LogInformation("✅ Analysis complete. Found {Count} incomplete records.", incompleteRecords.Count);

            Directory.CreateDirectory("output");

            var fullExportPath = Path.Combine("output", "Full_Patient_Export.csv");
            await using (var writer = new StreamWriter(fullExportPath))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(allPatients, stoppingToken);
            }

            var auditListPath = Path.Combine("output", "Incomplete_Demographics_Audit_List.csv");
            await using (var writer = new StreamWriter(auditListPath))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(incompleteRecords, stoppingToken);
            }

            logger.LogInformation("✅ Reports written: {FullExport} and {AuditExport}", fullExportPath, auditListPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error occurred during data audit.");
        }
    }

    private async System.Threading.Tasks.Task RunSyncAndSeedLogicAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- Seeding local OpenEMR with new test patients ---");

        try
        {
            var patientsToCreate = PatientDataGenerator.GeneratePatients(1000);
            var connectionString = "server=mariadb;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";

            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);

            var patientTable = await DetectPatientTableAsync(connection);
            logger.LogInformation("✅ Detected patient table: {Table}", patientTable);

            var deleteCmd = new MySqlCommand($"DELETE FROM {patientTable} WHERE pid > 0;", connection);
            await deleteCmd.ExecuteNonQueryAsync(stoppingToken);

            foreach (var patient in patientsToCreate)
            {
                if (stoppingToken.IsCancellationRequested) break;

                var insertCmd = new MySqlCommand($@"
                    INSERT INTO {patientTable} 
                    (pid, fname, lname, DOB, sex, street, city, state, postal_code, phone_cell)
                    VALUES (@pid, @fname, @lname, @dob, @sex, @street, @city, @state, @postal_code, @phone);", connection);

                insertCmd.Parameters.AddWithValue("@pid", patient.Pid);
                insertCmd.Parameters.AddWithValue("@fname", patient.FirstName);
                insertCmd.Parameters.AddWithValue("@lname", patient.LastName);
                insertCmd.Parameters.AddWithValue("@dob", patient.DateOfBirth);
                insertCmd.Parameters.AddWithValue("@sex", patient.Gender);
                insertCmd.Parameters.AddWithValue("@street", patient.StreetAddress);
                insertCmd.Parameters.AddWithValue("@city", patient.City);
                insertCmd.Parameters.AddWithValue("@state", patient.State);
                insertCmd.Parameters.AddWithValue("@postal_code", patient.PostalCode);
                insertCmd.Parameters.AddWithValue("@phone", patient.PhoneNumber);

                await insertCmd.ExecuteNonQueryAsync(stoppingToken);
            }

            logger.LogInformation("✅ Seeding complete: {Count} new patients created.", patientsToCreate.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error occurred while seeding the database.");
        }
    }

    private async System.Threading.Tasks.Task<bool> WaitForDatabaseAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Waiting for local database to be ready...");
        var connectionString = "server=mariadb;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                logger.LogInformation("✅ Database connection successful.");
                return true;
            }
            catch
            {
                if (attempt >= 30)
                {
                    logger.LogError("❌ Database unavailable after 30 attempts.");
                    return false;
                }
                await System.Threading.Tasks.Task.Delay(5000, stoppingToken);
            }
        }
        return false;
    }
}
