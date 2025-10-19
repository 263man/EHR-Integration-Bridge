using EhrBridge.Api.DataGeneration;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using EhrBridge.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EhrBridge.Api.Services;

/// <summary>
/// Business logic for controlling database operations such as reseeding.
/// Audit is now fully decoupled.
/// </summary>
public class ControlService : IControlService
{
    private readonly string _connectionString;
    private readonly ILogger<ControlService> _logger;

    private const int SeedCount = 1000;
    private const int StartPid = 100;

    public ControlService(IConfiguration configuration, ILogger<ControlService> logger)
    {
        var connectionString = configuration.GetConnectionString("EhrDatabase")
            ?? throw new InvalidOperationException("EhrDatabase connection string is missing.");

        _connectionString = connectionString;
        _logger = logger;

        TestDbConnection();
    }

    // Test database connection synchronously on service startup
    private void TestDbConnection()
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            connection.Open();
            connection.Close();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to connect to the EHR Database on startup. Check 'mariadb' service status and connection string.", ex);
        }
    }

    /// <summary>
    /// Cleans the patient_data table and inserts 1000 demo records.
    /// Audit is intentionally NOT triggered here.
    /// </summary>
    public async Task ReSeedDemoDataAsync()
    {
        _logger.LogInformation("ReSeedDemoDataAsync started.");

        await TruncatePatientDataAsync();
        var patients = PatientDataGenerator.GeneratePatients(SeedCount, StartPid);
        await InsertPatientDataAsync(patients);

        _logger.LogInformation("ReSeedDemoDataAsync completed successfully.");
    }

    private async Task TruncatePatientDataAsync()
    {
        const string sql = "TRUNCATE TABLE patient_data;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            using var command = new MySqlCommand(sql, connection) { CommandTimeout = 60 };

            _logger.LogInformation("Opening DB connection for TRUNCATE.");
            await connection.OpenAsync();
            int result = await command.ExecuteNonQueryAsync();

            _logger.LogInformation("TRUNCATE completed, result: {Result}", result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing TRUNCATE TABLE patient_data.");
            throw;
        }
    }

    private async Task InsertPatientDataAsync(List<Patient> patients)
    {
        const string insertSql =
            @"INSERT INTO patient_data 
              (pid, lname, fname, DOB, sex, street, city, state, postal_code, phone_cell, SS)
              VALUES
              (@pid, @lname, @fname, @DOB, @sex, @street, @city, @state, @postal_code, @phone_cell, @SS);";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            _logger.LogInformation("Opened DB connection for bulk insert: {Count} records.", patients.Count);

            foreach (var p in patients)
            {
                using var command = new MySqlCommand(insertSql, connection);
                command.CommandTimeout = 60;

                command.Parameters.AddWithValue("@pid", p.Pid);
                command.Parameters.AddWithValue("@lname", p.LastName);
                command.Parameters.AddWithValue("@fname", p.FirstName);
                command.Parameters.AddWithValue("@DOB", p.DateOfBirth);
                command.Parameters.AddWithValue("@sex", p.Sex);
                command.Parameters.AddWithValue("@street", p.StreetAddress);
                command.Parameters.AddWithValue("@city", p.City);
                command.Parameters.AddWithValue("@state", p.State);
                command.Parameters.AddWithValue("@postal_code", p.PostalCode);
                command.Parameters.AddWithValue("@phone_cell", (object?)p.PhoneNumber ?? DBNull.Value);
                command.Parameters.AddWithValue("@SS", p.SocialSecurityNumber);

                await command.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("InsertPatientDataAsync: inserted {Count} records.", patients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting patient records.");
            throw;
        }
    }
}
