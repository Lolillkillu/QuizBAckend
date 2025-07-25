using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizzWebApp.Models
{
    public class QuizStatistics
    {
        [Key]
        public int QuizStatisticsId { get; set; }

        [ForeignKey("UserModel")]
        public int UserId { get; set; }
        public virtual UserModel? User { get; set; }

        [ForeignKey("QuizzModel")]
        public int QuizzId { get; set; }
        public virtual QuizzModel? Quiz { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public DateTime DateCompleted { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public double ScorePercentage => TotalQuestions == 0
            ? 0
            : Math.Round((double)CorrectAnswers / TotalQuestions * 100, 2);
    }
}
