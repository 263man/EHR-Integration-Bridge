using Bogus;
using System.Collections.Generic;
using System; // Required for DateTime

namespace EhrIntegrationBridge
{
    public static class PatientDataGenerator
    {
        // MODIFIED: Accepts the dynamic startPid value from the Worker.cs query
        public static List<Patient> GeneratePatients(int count, int startPid)
        {
            // Use the passed-in starting PID
            int patientId = startPid; 

            var patientFaker = new Faker<Patient>()
                // Use the incremented ID for the database's 'pid' column
                .RuleFor(p => p.Pid, f => patientId++) 
                .RuleFor(p => p.FirstName, f => f.Name.FirstName())
                .RuleFor(p => p.LastName, f => f.Name.LastName())
                // FIX (Previous): Explicitly generate a sex ('m' or 'f')
                .RuleFor(p => p.Sex, f => f.PickRandom(new[] { "m", "f" }))
                // 💡 NEW FIX: Generate a unique, placeholder SSN to satisfy the completeness check.
                .RuleFor(p => p.SocialSecurityNumber, (f, p) => $"999-00-{p.Pid}") 
                // FIX CS1061: Simplifies DOB generation to a past date (between 18 and 80 years ago)
                .RuleFor(p => p.DateOfBirth, f => f.Date.Past(80, DateTime.Now.AddYears(-18)))
                .RuleFor(p => p.StreetAddress, f => f.Address.StreetAddress())
                .RuleFor(p => p.City, f => f.Address.City())
                .RuleFor(p => p.State, f => f.Address.StateAbbr())
                .RuleFor(p => p.PostalCode, f => f.Address.ZipCode())
                // Set the phone number to null 20% of the time to simulate incomplete data
                .RuleFor(p => p.PhoneNumber, f => f.Random.Replace("###-###-####").OrNull(f, .20f)); 

            return patientFaker.Generate(count);
        }
    }

    public class Patient
    {
        public int Pid { get; set; } 
        // FIX CS8618: Mark all string properties as nullable (string?)
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Sex { get; set; }
        public string? SocialSecurityNumber { get; set; } // 💡 NEW: Property for SSN
        public System.DateTime DateOfBirth { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? PhoneNumber { get; set; }
    }
}