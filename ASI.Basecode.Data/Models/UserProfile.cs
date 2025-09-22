using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class UserProfile
    {
        [Key]
        [ForeignKey("User")]   
        public int UserId { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? MiddleName { get; set; }
        public string? Suffix { get; set; }
        public string? MobileNo { get; set; }
        public string HomeAddress { get; set; }
        public string Province { get; set; }
        public string Municipality { get; set; }
        public string Barangay { get; set; }
        public DateTime DateOfBirth { get; set; }  
        public string PlaceOfBirth { get; set; }
        public int Age { get; set; }                
        public string MaritalStatus { get; set; }
        public string Gender { get; set; }
        public string? Religion { get; set; }
        public string Citizenship { get; set; }


        public User User { get; set; }
    }
}
