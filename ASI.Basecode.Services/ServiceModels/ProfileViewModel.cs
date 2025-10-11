using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.ServiceModels
{
    public class ProfileViewModel
    {
        // Users table
        public int UserId { get; set; }
        public string IdNumber { get; set; }
        public string Role { get; set; } 
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }

        // UserProfiles table
        public string ProfilePictureUrl { get; set; }
        public IFormFile? ProfilePhotoFile { get; set; }
        public string MiddleName { get; set; }
        public string Suffix { get; set; }
        public string MobileNo { get; set; }
        public string HomeAddress { get; set; }
        public string Province { get; set; }
        public string Municipality { get; set; }
        public string Barangay { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string PlaceOfBirth { get; set; }
        public int? Age { get; set; }
        public string MaritalStatus { get; set; }
        public string Gender { get; set; }
        public string Religion { get; set; }
        public string Citizenship { get; set; }
    }

    public class StudentProfileViewModel : ProfileViewModel
    {
        // Students table
        public int StudentId { get; set; }
        public string AdmissionTypeDb { get; set; }
        public string AdmissionType
        {
            get
            {
                return AdmissionTypeDb switch
                {
                    "Old Student" => "Old",
                    "New Student" => "New",
                    "Transferee" => "Transferee",
                    _ => AdmissionTypeDb
                };
            }
            set
            {
                AdmissionTypeDb = value switch
                {
                    "Old" => "Old Student",
                    "New" => "New Student",
                    "Transferee" => "Transferee",
                    _ => value
                };
            }
        }
        public string ProgramDb { get; set; }
        public string Program
        {
            get
            {
                return ProgramDb switch
                {
                    "BSCS" => "Computer Science",
                    "BSIT" => "Information Technology",
                    "BSIS" => "Information Systems",
                    _ => ProgramDb
                };
            }
            set
            {
                ProgramDb = value switch
                {
                    "Computer Science" => "BSCS",
                    "Information Technology" => "BSIT",
                    "Information Systems" => "BSIS",
                    _ => value
                };
            }
        }
        public string Department { get; set; }
        public string YearLevel { get; set; }
        public string StudentStatus { get; set; }
    }

    public class TeacherProfileViewModel : ProfileViewModel
    {
        // Teachers table
        public int TeacherId { get; set; }
        public string Position { get; set; }
        public string Department { get; set; }
    }
}
