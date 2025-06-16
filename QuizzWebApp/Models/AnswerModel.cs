using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizzWebApp.Models
{
    public class AnswerModel
    {
        [Key]
        public int AnswerId { get; set; }
        [ForeignKey("QuestionModel")]
        public int QuestionId { get; set; }
        public QuestionModel? Question { get; set; }
        public required string Answer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
