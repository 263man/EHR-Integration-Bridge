namespace EhrIntegrationBridge;

public class CleanPatientRecord
{
    public long PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string StreetAddress { get; internal set; } = string.Empty;
    public string PostalCode { get; internal set; } = string.Empty;
    public string City { get; internal set; } = string.Empty;
    public string State { get; internal set; } = string.Empty;
}