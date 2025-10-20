using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Login View Model
    /// </summary>
    public class LoginViewModel
    {
      
        [JsonPropertyName("idNumber")]
        [Required(ErrorMessage = "ID Number is required.")]
        public string IdNumber { get; set; }

        [JsonPropertyName("password")]
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }

        [JsonPropertyName("rememberMe")]
        public bool RememberMe { get; set; }
    }

    public class StudentLoginViewModel : LoginViewModel
    {
        [Required(ErrorMessage = "Student ID required.")]
        public new string IdNumber { get; set; }
    }

    public class TeacherLoginViewModel : LoginViewModel
    {
        [Required(ErrorMessage = "Teacher ID required.")]
        public new string IdNumber { get; set; }
    }

    public class AdminLoginViewModel : LoginViewModel
    {
        [Required(ErrorMessage = "Admin ID required.")]
        public new string IdNumber { get; set; }
    }
}
