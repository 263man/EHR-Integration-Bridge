using Bogus;
using System.Collections.Generic;
using System;

namespace EhrBridge.Api.DataGeneration
{
    public static class PatientDataGenerator
    {
        public static List<Patient> GeneratePatients(int count, int startPid)
        {
            int patientId = startPid;

            var patientFaker = new Faker<Patient>()
                .RuleFor(p => p.Pid, f => patientId++)
                .RuleFor(p => p.FirstName, f => f.Name.FirstName())
                .RuleFor(p => p.LastName, f => f.Name.LastName())
                .RuleFor(p => p.Sex, f => f.PickRandom(new[] { "m", "f" }))
                .RuleFor(p => p.SocialSecurityNumber, (f, p) => $"999-00-{p.Pid}")
                .RuleFor(p => p.DateOfBirth, f => f.Date.Past(80, DateTime.Now.AddYears(-18)))
                // ✅ Always ensure non-null address fields
                .RuleFor(p => p.StreetAddress, f => f.Address.StreetAddress() ?? "Unknown Street")
                .RuleFor(p => p.City, f => f.Address.City() ?? "Unknown City")
                .RuleFor(p => p.State, f => f.Address.StateAbbr() ?? "NA")
                .RuleFor(p => p.PostalCode, f => f.Address.ZipCode() ?? "00000")
                // 20% missing phone numbers to simulate incomplete data
                .RuleFor(p => p.PhoneNumber, f => f.Random.Replace("###-###-####").OrNull(f, 0.20f));

            return patientFaker.Generate(count);
        }
    }

    public class Patient
    {
        public int Pid { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Sex { get; set; }
        public string? SocialSecurityNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? StreetAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
