using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace QuizzWebApp.Models
{
    public class QuestionModel
    {
        [Key]
        public int QuestionId { get; set; }
        [ForeignKey("QuizzModel")]
        public int QuizzId { get; set; }
        public QuizzModel? Quizz { get; set; }
        public string Question { get; set; }
        public ICollection<AnswerModel>? Answers { get; set; }
    }
}
