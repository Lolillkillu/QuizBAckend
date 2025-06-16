using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizzWebApp.Models
{
    public class QuizzModel
    {
        [Key]
        public int QuizzId { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        [ForeignKey("ScienceModel")]
        public int? ScienceId { get; set; }
        public required string Author { get; set; }
        public ICollection<QuestionModel>? Questions { get; set; }
        public ScienceModel? Science { get; internal set; }
    }
}
