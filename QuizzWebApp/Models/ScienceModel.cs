using System.ComponentModel.DataAnnotations;

namespace QuizzWebApp.Models
{
    public class ScienceModel
    {
        [Key]
        public int ScienceId { get; set; }
        public required string ScienceName { get; set; }
    }
}
