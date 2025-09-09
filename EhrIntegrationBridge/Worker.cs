using Hl7.Fhir.Rest;
using Hl7.Fhir.Model;
// This alias resolves the name collision between our old Patient class and the FHIR Patient model.
using FhirPatient = Hl7.Fhir.Model.Patient;

namespace EhrIntegrationBridge;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    private const string OpenEmrFhirEndpoint = "http://openemr/apis/default/fhir";

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("--- EHR BRIDGE WORKER STARTING (FHIR EXTRACTION MODE) ---");

        if (!await WaitForFhirEndpointAsync(stoppingToken))
        {
            logger.LogCritical("FHIR endpoint did not become available. Shutting down.");
            return;
        }

        try
        {
            logger.LogInformation("--- Connecting to FHIR endpoint at {Endpoint} ---", OpenEmrFhirEndpoint);
            var fhirClient = new FhirClient(OpenEmrFhirEndpoint)
            {
                Settings = { VerifyFhirVersion = false }
            };

            logger.LogInformation("--- Attempting to read Patient with ID '1' ---");
            
            var patient = await fhirClient.ReadAsync<FhirPatient>("Patient/1");

            if (patient != null)
            {
                var patientName = patient.Name.FirstOrDefault();
                var fullName = $"{patientName?.Given.FirstOrDefault()} {patientName?.Family}";
                
                logger.LogInformation("✅ --- FHIR READ SUCCESS ---");
                logger.LogInformation("   - ID: {PatientId}", patient.Id);
                logger.LogInformation("   - Name: {PatientName}", fullName.Trim());
                logger.LogInformation("   - DoB: {PatientBirthDate}", patient.BirthDate);
            }
            else
            {
                logger.LogWarning("--- FHIR READ FAILED: Patient with ID '1' not found. ---");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred during FHIR interaction.");
        }
    }

    private async System.Threading.Tasks.Task<bool> WaitForFhirEndpointAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;
        using var httpClient = new HttpClient();
        var metadataUrl = $"{OpenEmrFhirEndpoint}/metadata";

        while (stoppingToken.IsCancellationRequested == false)
        {
            try
            {
                attempt++;
                logger.LogInformation("Attempting to connect to FHIR metadata endpoint... (Attempt {attempt})", attempt);
                var response = await httpClient.GetAsync(metadataUrl, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("✅ FHIR endpoint is responsive.");
                    return true;
                }
                logger.LogWarning("FHIR endpoint returned non-success status: {StatusCode}", response.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning("FHIR endpoint connection failed. Error: {ErrorMessage}", ex.Message.Split(Environment.NewLine)[0]);
            }

            if (attempt >= 30)
            {
                logger.LogError("Could not connect to the FHIR endpoint after multiple attempts.");
                return false;
            }
            await System.Threading.Tasks.Task.Delay(5000, stoppingToken);
        }
        return false;
    }
}