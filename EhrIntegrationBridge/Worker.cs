using Hl7.Fhir.Rest;
using FhirPatient = Hl7.Fhir.Model.Patient;

namespace EhrIntegrationBridge;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private const string FhirServerEndpoint = "http://hapi.fhir.org/baseR4";

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- EHR BRIDGE WORKER STARTING (PUBLIC FHIR SERVER MODE) ---");

        await System.Threading.Tasks.Task.Delay(5000, stoppingToken);

        try
        {
            var fhirClient = new FhirClient(FhirServerEndpoint)
            {
                Settings = { VerifyFhirVersion = false, PreferredFormat = Hl7.Fhir.Rest.ResourceFormat.Json }
            };

            logger.LogInformation("--- Searching for Patient resources on public server: {Endpoint} ---", FhirServerEndpoint);
            
            var searchParams = new SearchParams().LimitTo(15);
            var result = await fhirClient.SearchAsync<FhirPatient>(searchParams, stoppingToken);
            
            // This more robust check will satisfy the compiler and prevent any possibility of a null reference crash.
            if (result?.Entry is not null && result.Entry.Any())
            {
                logger.LogInformation("✅ --- FHIR SEARCH COMPLETE ---");
                logger.LogInformation("   - Found {Count} Patient resources.", result.Entry.Count);
                logger.LogInformation("--- Displaying patients (with robust name handling) ---");

                foreach (var entry in result.Entry)
                {
                    if (entry.Resource is FhirPatient patient)
                    {
                        var patientName = patient.Name.FirstOrDefault();
                        string givenNames = (patientName?.Given.Any() == true) ? string.Join(" ", patientName.Given) : "[NO GIVEN NAME]";
                        string familyName = patientName?.Family ?? "[NO FAMILY NAME]";
                        string fullName = $"{givenNames} {familyName}";
                        
                        logger.LogInformation("   -> Patient: {PatientName} (ID: {PatientId})", fullName.Trim(), patient.Id);
                    }
                }
            }
            else
            {
                logger.LogInformation("✅ --- FHIR SEARCH COMPLETE ---");
                logger.LogInformation("   - Found 0 Patient resources.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during FHIR interaction.");
        }
    }
}