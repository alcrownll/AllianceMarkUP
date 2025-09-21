using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Login View Model
    /// </summary>
    public class MarkupLoginViewModel
    {
        /// For student login
        /// <summary>ユーザーID</summary>
        [JsonPropertyName("studentId")]
        [Required(ErrorMessage = "Student ID is required.")]
        public string UserId { get; set; }

        /// For teacher login
        [JsonPropertyName("teacherId")]
        [Required(ErrorMessage = "Teacher ID is required.")]
        public string teacherId { get; set; }

        /// For admin login
        [JsonPropertyName("adminId")]
        [Required(ErrorMessage = "Admin ID is required.")]
        public string adminId { get; set; }

        /// <summary>パスワード</summary>
        [JsonPropertyName("Password")]
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }
    }
}
