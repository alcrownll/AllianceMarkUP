using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// Login View Model
    /// </summary>
    public class LoginViewModel
    {
        /*/// <summary>ユーザーID</summary>
        [JsonPropertyName("userId")]
        [Required(ErrorMessage = "UserId is required.")]
        public string User { get; set; }*/

        /// <summary>ユーザーID</summary>
        [JsonPropertyName("idNumber")]
        [Required(ErrorMessage = "ID Number is required.")]
        public string IdNumber { get; set; }

        /// <summary>パスワード</summary>
        [JsonPropertyName("password")]
        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; }

        [JsonPropertyName("rememberMe")]
        public bool RememberMe { get; set; }
    }
}
