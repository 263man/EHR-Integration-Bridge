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
        {
            await RunSyncAndSeedLogicAsync(stoppingToken);
        }
        else
        {
            await RunDataQualityAuditAsync(stoppingToken);
        }
        
        logger.LogInformation("--- EHR BRIDGE WORKER FINISHED ---");
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

            var command = new MySqlCommand("SELECT pid, fname, lname, DOB, sex, phone_cell, street FROM patient_data WHERE pid > 0;", connection);
            await using var reader = await command.ExecuteReaderAsync(stoppingToken);

            while (await reader.ReadAsync(stoppingToken))
            {
                var patientId = reader.GetInt64("pid");
                var firstName = reader.GetString("fname");
                var lastName = reader.GetString("lname");
                var dob = reader.GetDateTime("DOB").ToString("yyyy-MM-dd");
                var gender = reader.GetString("sex");
                var phone = reader.GetString("phone_cell");
                var street = reader.GetString("street");

                // Add to the full export list
                allPatients.Add(new CleanPatientRecord { PatientId = patientId, FirstName = firstName, LastName = lastName, DateOfBirth = dob, Gender = gender, PhoneNumber = phone });

                // Business Logic: Find patients with missing data
                if (string.IsNullOrEmpty(street) || string.IsNullOrEmpty(phone))
                {
                    incompleteRecords.Add(new IncompleteDemographicRecord
                    {
                        PatientId = patientId,
                        FirstName = firstName,
                        LastName = lastName,
                        MissingDataFlag = string.IsNullOrEmpty(street) ? "Missing Address" : "Missing Phone"
                    });
                }
            }
            logger.LogInformation("✅ Analysis complete. Found {Count} records with incomplete demographics.", incompleteRecords.Count);

            // Write the full, clean export
            var fullExportPath = Path.Combine("output", "Full_Patient_Export.csv");
            Directory.CreateDirectory("output");
            await using (var writer = new StreamWriter(fullExportPath))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(allPatients, stoppingToken);
            }
            logger.LogInformation("   - Successfully wrote {Count} total records to {Path}", allPatients.Count, fullExportPath);

            // Write the actionable audit list
            var auditListPath = Path.Combine("output", "Incomplete_Demographics_Audit_List.csv");
            await using (var writer = new StreamWriter(auditListPath))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(incompleteRecords, stoppingToken);
            }
            logger.LogInformation("✅ --- AUDIT REPORT GENERATED ---");
            logger.LogInformation("   - Successfully wrote {Count} actionable records to {Path}", incompleteRecords.Count, auditListPath);

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during the data quality audit.");
        }
    }

    private async System.Threading.Tasks.Task RunSyncAndSeedLogicAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- Seeding local OpenEMR with new, messy patient data... ---");
        try 
        {
            var patientsToCreate = PatientDataGenerator.GeneratePatients(1000);
            var connectionString = "server=mariadb;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);
            
            var deleteCmd = new MySqlCommand("DELETE FROM patient_data WHERE pid > 0;", connection);
            await deleteCmd.ExecuteNonQueryAsync(stoppingToken);

            foreach (var patient in patientsToCreate) 
            {
                if (stoppingToken.IsCancellationRequested) break;
                var command = new MySqlCommand(
                    "INSERT INTO patient_data (pid, fname, lname, DOB, sex, street, city, state, postal_code, phone_cell) " +
                    "VALUES (@pid, @fname, @lname, @dob, @sex, @street, @city, @state, @postal_code, @phone);",
                    connection);
                
                command.Parameters.AddWithValue("@pid", patient.Pid);
                command.Parameters.AddWithValue("@fname", patient.FirstName);
                command.Parameters.AddWithValue("@lname", patient.LastName);
                command.Parameters.AddWithValue("@dob", patient.DateOfBirth);
                command.Parameters.AddWithValue("@sex", patient.Gender);
                command.Parameters.AddWithValue("@street", patient.StreetAddress);
                command.Parameters.AddWithValue("@city", patient.City);
                command.Parameters.AddWithValue("@state", patient.State);
                command.Parameters.AddWithValue("@postal_code", patient.PostalCode);
                command.Parameters.AddWithValue("@phone", patient.PhoneNumber);
                
                await command.ExecuteNonQueryAsync(stoppingToken);
            }
            logger.LogInformation("✅ --- DATA SEEDING COMPLETE: {count} patients created in OpenEMR. ---", patientsToCreate.Count);
        } 
        catch (Exception ex) 
        {
            logger.LogError(ex, "An error occurred while seeding the local database.");
        }
    }

    private async System.Threading.Tasks.Task<bool> WaitForDatabaseAsync(CancellationToken stoppingToken) 
    {
        logger.LogInformation("Waiting for local database service to be ready...");
        var connectionString = "server=mariadb;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";
        var attempt = 0;
        while (stoppingToken.IsCancellationRequested == false) 
        {
            try 
            {
                attempt++;
                await using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                logger.LogInformation("✅ Database connection successful.");
                return true;
            } 
            catch (Exception) 
            {
                if (attempt >= 30) 
                {
                    logger.LogError("Could not connect to the database after 30 attempts.");
                    return false; 
                }
                await System.Threading.Tasks.Task.Delay(5000, stoppingToken);
            }
        }
        return false;
    }
}