using MySqlConnector;
namespace EhrIntegrationBridge;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EHR Integration Bridge starting at: {time}", DateTimeOffset.Now);

        // --- Step 1: Generate Data ---
        var patientsToCreate = PatientDataGenerator.GeneratePatients(1000);
        logger.LogInformation("{count} patient records generated.", patientsToCreate.Count);

        // --- Step 2: Connect to Database and Create Patients ---
        var connectionString = "server=localhost;port=3306;database=openemr;user=openemr;password=openemrpass;SslMode=None;";
        logger.LogInformation("Connecting to MariaDB database...");

        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(stoppingToken);
            logger.LogInformation("Database connection successful.");

            int createdCount = 0;
            foreach (var patient in patientsToCreate)
            {
                if (stoppingToken.IsCancellationRequested) break;

                // OpenEMR's patient table is `patient_data`. We will insert into it.
                var command = new MySqlCommand(
                    "INSERT INTO patient_data (fname, lname, DOB, street, city, state, postal_code, phone_cell) " +
                    "VALUES (@fname, @lname, @dob, @street, @city, @state, @postal_code, @phone);",
                    connection);

                command.Parameters.AddWithValue("@fname", patient.FirstName);
                command.Parameters.AddWithValue("@lname", patient.LastName);
                command.Parameters.AddWithValue("@dob", patient.DateOfBirth);
                command.Parameters.AddWithValue("@street", patient.StreetAddress);
                command.Parameters.AddWithValue("@city", patient.City);
                command.Parameters.AddWithValue("@state", patient.State);
                command.Parameters.AddWithValue("@postal_code", patient.PostalCode);
                command.Parameters.AddWithValue("@phone", patient.PhoneNumber);

                await command.ExecuteNonQueryAsync(stoppingToken);
                createdCount++;
            }

            logger.LogInformation("--- Data Seeding Complete: {count} patients created in OpenEMR. ---", createdCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while creating patients in the database.");
        }

        await StopAsync(stoppingToken);
    }
}