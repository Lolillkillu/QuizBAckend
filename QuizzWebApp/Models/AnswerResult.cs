namespace QuizzWebApp.Models
{
    public class AnswerResult
    {
        public bool IsCorrect { get; set; }
        public int CorrectAnswerId { get; set; }
        public string CorrectAnswerText { get; set; }
    }
}
