using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.WebApp.Models
{
    // Forgot Password Step 1
    public class ResetPasswordRequestViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Email address must be associated with a registered account.")]
        public string Email { get; set; } = "";
    }

    // Forgot Password Step 2
    public class ResetPasswordViewModel
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}
