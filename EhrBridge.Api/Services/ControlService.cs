using EhrBridge.Api.DataGeneration;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
// Removed: using EhrBridge.Api.Models; // This namespace is now empty/obsolete
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EhrBridge.Api.Services;

/// <summary>
/// Business logic for controlling database operations such as reseeding.
/// Audit is now fully decoupled.
/// Includes automatic schema alignment for OpenEMR compatibility.
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

    // Ensure DB connectivity early
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
    /// Cleans the patient_data table, aligns schema, and inserts 1000 demo records.
    /// Audit remains intentionally decoupled.
    /// </summary>
    public async Task ReSeedDemoDataAsync()
    {
        _logger.LogInformation("ReSeedDemoDataAsync started.");

        // Always ensure table schema allows NULL for phone_cell
        await EnsurePhoneCellNullableAsync();

        await TruncatePatientDataAsync();
        var patients = PatientDataGenerator.GeneratePatients(SeedCount, StartPid);
        await InsertPatientDataAsync(patients);

        _logger.LogInformation("ReSeedDemoDataAsync completed successfully.");
    }

    /// <summary>
    /// Ensures the patient_data.phone_cell column is nullable to support generated data.
    /// </summary>
    private async Task EnsurePhoneCellNullableAsync()
    {
        const string sql =
            @"ALTER TABLE patient_data MODIFY phone_cell VARCHAR(255) NULL;";

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();

            _logger.LogInformation("Ensured patient_data.phone_cell is nullable.");
        }
        catch (Exception ex)
        {
            // Suppress benign errors if already NULL
            if (ex.Message.Contains("Invalid default value") || ex.Message.Contains("NULL"))
            {
                _logger.LogWarning("Column phone_cell already nullable or migration not required: {Message}", ex.Message);
                return;
            }

            _logger.LogError(ex, "Error ensuring patient_data.phone_cell is nullable.");
            throw;
        }
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