namespace QuizzWebApp.Models
{
    public class Player
    {
        public string ConnectionId { get; set; }
        public string PlayerId { get; set; }
        public string Name { get; set; }
        public int Score { get; set; } = 0;
        public bool IsHost { get; set; }
        public int CurrentQuestionIndex { get; set; } = 0;
        public bool HasCompleted { get; set; } = false;
        public bool IsReady { get; set; }
        public List<PlayerAnswer> Answers { get; } = new List<PlayerAnswer>();
    }
}
