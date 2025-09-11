// fhir-evidence/Program.cs

using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using System.Globalization;
using CsvHelper;

namespace FhirEvidenceGenerator;

class Program
{
    static async System.Threading.Tasks.Task Main(string[] args)
    {
        Console.WriteLine("--- FHIR Evidence Generator Starting ---");
        
        var fhirServerUrl = "http://hapi.fhir.org/baseR4";
        var patientRecords = new List<FhirPatientRecord>();

        try
        {
            Console.WriteLine($"Attempting to connect to public FHIR server: {fhirServerUrl}");
            var client = new FhirClient(fhirServerUrl);
            
            // Search for the first 10 patients to use as evidence
            var searchParams = new SearchParams().LimitTo(10);
            Bundle? results = await client.SearchAsync<Patient>(searchParams);
            Console.WriteLine("✅ Successfully received data from HAPI FHIR server.");

            if (results != null)
            {
                foreach (var entry in results.Entry)
                {
                    if (entry.Resource is Patient patient)
                    {
                        patientRecords.Add(new FhirPatientRecord
                        {
                            PatientId = patient.Id ?? "N/A",
                            // Safely get the first given name and family name
                            FullName = patient.Name.FirstOrDefault()?.Given.FirstOrDefault() + " " + patient.Name.FirstOrDefault()?.Family ?? "Unknown",
                            Gender = patient.Gender?.ToString() ?? "N/A",
                            DateOfBirth = patient.BirthDate ?? "N/A"
                        });
                    }
                }
            }
            
            var outputPath = "Hapi_Fhir_Patient_Export.csv";
            await using (var writer = new StreamWriter(outputPath))
            await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                await csv.WriteRecordsAsync(patientRecords);
            }

            Console.WriteLine($"✅ --- SUCCESS: Wrote {patientRecords.Count} records to {outputPath} ---");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ --- ERROR: An error occurred ---");
            Console.WriteLine(ex.Message);
        }
    }
}