using System.ComponentModel.DataAnnotations;

namespace QuizzWebApp.Models
{
    public class UserModel
    {
        [Key]
        public int UserId { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Email { get; set; }
        public required bool IsAdmin { get; set; } = false;

    }
}
