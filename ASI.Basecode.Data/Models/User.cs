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

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; }

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; }

        [Required]
        [MaxLength(255)]
        public string Email { get; set; }

        [Required]
        [MaxLength(50)]
        public string IdNumber { get; set; }

        [Required]
        [MaxLength(255)]
        public string Password { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; }

        [Required]
        [MaxLength(20)]
        public string AccountStatus { get; set; }

        [Column(TypeName = "timestamp")]
        public DateTime CreatedAt { get; set; }

        [Column(TypeName = "timestamp")]
        public DateTime UpdatedAt { get; set; }

        // ==============================
        // Relationships
        // ==============================
        public Student Student { get; set; }
        public Teacher Teacher { get; set; }
        public UserProfile UserProfile { get; set; }

        // Notifications (1:N)
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // Calendar Events (1:N)
        public ICollection<CalendarEvent> CalendarEvents { get; set; } = new List<CalendarEvent>();

        // Password Reset Tokens (1:N)
        public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    }
}
