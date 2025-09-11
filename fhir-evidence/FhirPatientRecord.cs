// fhir-evidence/FhirPatientRecord.cs

namespace FhirEvidenceGenerator;

public class FhirPatientRecord
{
    public string PatientId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
}