namespace EhrBridge.Api.Models
{
    public class IncompleteRecord
    {
        public int PatientId { get; set; }
        public string Field { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> MissingFields { get; set; } = new();
    }
}
