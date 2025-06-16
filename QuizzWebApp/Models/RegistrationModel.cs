using System.ComponentModel.DataAnnotations;

namespace QuizzWebApp.Models
{
    public class RegistrationModel
    {
        public required string Username { get; set; }
        [EmailAddress]
        public required string Email { get; set; }
        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; set; }
    }
}
