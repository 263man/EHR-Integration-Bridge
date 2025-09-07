using Bogus;

namespace EhrIntegrationBridge;

public class Patient
{
    public int PatientId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}

public static class PatientDataGenerator
{
    public static List<Patient> GeneratePatients(int count)
    {
        int patientIdSeed = 1;
        var patientFaker = new Faker<Patient>()
            .RuleFor(p => p.PatientId, f => patientIdSeed++)
            .RuleFor(p => p.FirstName, f => f.Name.FirstName())
            .RuleFor(p => p.LastName, f => f.Name.LastName())
            .RuleFor(p => p.DateOfBirth, f => f.Date.Past(50, DateTime.Now.AddYears(-18)))
            .RuleFor(p => p.Gender, f => f.PickRandom("Male", "Female", "Other"))
            .RuleFor(p => p.StreetAddress, f => f.Address.StreetAddress())
            .RuleFor(p => p.City, f => f.Address.City())
            .RuleFor(p => p.State, f => f.Address.StateAbbr())
            .RuleFor(p => p.PostalCode, f => f.Address.ZipCode())
            .RuleFor(p => p.PhoneNumber, f => f.Phone.PhoneNumber());

        return patientFaker.Generate(count);
    }
}