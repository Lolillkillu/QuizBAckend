namespace QuizzWebApp.Models
{
    public class PlayerAnswer
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public List<int> AnswerIds { get; set; } = new List<int>();
        public List<string> AnswerTexts { get; set; } = new List<string>();
        public bool IsCorrect { get; set; }
    }
}
