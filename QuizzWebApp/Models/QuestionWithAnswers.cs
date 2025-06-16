namespace QuizzWebApp.Models
{
    public class QuestionWithAnswers
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; }
        public List<AnswerDto> Answers { get; set; }
    }
}
