namespace QuizzWebApp.Models
{
    public class SubmitGameResult
    {
        public string Username { get; set; } = default!;
        public int QuizId { get; set; }
        public int CorrectAnswers { get; set; }
        public int TotalQuestions { get; set; }
    }
}
