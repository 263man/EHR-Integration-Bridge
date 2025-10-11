namespace EhrBridge.Api.Data;

// WARNING: This DTO contains Protected Health Information (PHI)
// This data is only exposed to enable administrative follow-up on incomplete records.
// In a production environment, this API MUST be secured with HTTPS and robust Authorization.
public class IncompleteRecordDto
{
    // PatientId is critical for identifying the record in the EHR for correction.
    public long PatientId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    
    // A flag to clearly state why the record failed the audit.
    public string MissingDataFlag { get; set; } = string.Empty;
}
