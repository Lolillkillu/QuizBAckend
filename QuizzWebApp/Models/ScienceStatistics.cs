using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuizzWebApp.Models
{
    public class ScienceStatistics
    {
        [Key]
        public int ScienceStatisticsId { get; set; }

        [ForeignKey("UserModel")]
        public int UserId { get; set; }
        public virtual UserModel? User { get; set; }

        [ForeignKey("ScienceModel")]
        public int ScienceId { get; set; }
        public virtual ScienceModel? Science { get; set; }
        public int TotalQuizzesTaken { get; set; }
        public int TotalQuestionsAnswered { get; set; }
        public int TotalCorrectAnswers { get; set; }

        [NotMapped]
        public double OverallAccuracy => TotalQuestionsAnswered == 0
            ? 0
            : Math.Round((double)TotalCorrectAnswers / TotalQuestionsAnswered * 100, 2);
    }
}
