using Hl7.Fhir.Rest;
using CsvHelper;
using System.Globalization;
using FhirPatient = Hl7.Fhir.Model.Patient;

namespace EhrIntegrationBridge;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private const string FhirServerEndpoint = "http://hapi.fhir.org/baseR4";

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- EHR BRIDGE WORKER STARTING (CSV GENERATION MODE) ---");

        await System.Threading.Tasks.Task.Delay(5000, stoppingToken);

        try
        {
            var fhirClient = new FhirClient(FhirServerEndpoint)
            {
                Settings = { VerifyFhirVersion = false, PreferredFormat = Hl7.Fhir.Rest.ResourceFormat.Json }
            };

            logger.LogInformation("--- Searching for Patient resources on {Endpoint} ---", FhirServerEndpoint);
            
            var searchParams = new SearchParams().LimitTo(50);
            var result = await fhirClient.SearchAsync<FhirPatient>(searchParams, stoppingToken);
            
            var cleanRecords = new List<CleanPatientRecord>();
            
            if (result?.Entry is not null && result.Entry.Any())
            {
                logger.LogInformation("--- Transforming {Count} FHIR Patient resources... ---", result.Entry.Count);
                foreach (var entry in result.Entry)
                {
                    if (entry.Resource is FhirPatient patient)
                    {
                        var patientName = patient.Name.FirstOrDefault();
                        cleanRecords.Add(new CleanPatientRecord
                        {
                            PatientId = patient.Id,
                            FirstName = (patientName?.Given.Any() == true) ? string.Join(" ", patientName.Given) : "[MISSING]",
                            LastName = patientName?.Family ?? "[MISSING]",
                            DateOfBirth = patient.BirthDate,
                            Gender = patient.Gender?.ToString() ?? "[MISSING]"
                        });
                    }
                }
                logger.LogInformation("✅ Data transformation complete.");

                // --- NEW: Write the clean records to a CSV file ---
                var outputPath = Path.Combine("output", "Clean_Claims_Export.csv");
                Directory.CreateDirectory("output"); // Ensure the directory exists

                using (var writer = new StreamWriter(outputPath))
                using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                {
                    csv.WriteRecords(cleanRecords);
                }
                logger.LogInformation("✅ --- CSV FILE GENERATED ---");
                logger.LogInformation("   - Successfully wrote {Count} records to {Path}", cleanRecords.Count, outputPath);
            }
            else
            {
                logger.LogInformation("--- Found 0 Patient resources to process. ---");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during the process.");
        }
    }
}