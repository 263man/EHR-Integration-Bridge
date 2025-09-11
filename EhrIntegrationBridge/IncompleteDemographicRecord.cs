namespace EhrIntegrationBridge;

public class IncompleteDemographicRecord
{
    public long PatientId { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string MissingDataFlag { get; set; } = string.Empty;
}