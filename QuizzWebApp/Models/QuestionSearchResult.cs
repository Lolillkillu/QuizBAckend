using System.Text.Json.Serialization;

namespace QuizzWebApp.Models
{
    public class QuestionSearchResult
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public int QuizzId { get; set; }
        public string QuizzTitle { get; set; } = "Empty";
    }
}
