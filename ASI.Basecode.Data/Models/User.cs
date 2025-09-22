using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASI.Basecode.Data.Models
{
    public partial class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }
        public string FirstName { get; set; }   
        public string LastName { get; set; }  
        public string Email { get; set; }
        public string IdNumber { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        public string AccountStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Student Student { get; set; }
        public Teacher Teacher { get; set; }
        public UserProfile UserProfile { get; set; }
    }
}
