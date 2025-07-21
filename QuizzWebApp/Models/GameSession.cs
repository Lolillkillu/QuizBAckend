namespace QuizzWebApp.Models
{
    public class GameSession
    {
        public string GameId { get; } = Guid.NewGuid().ToString();
        public GameStatus Status { get; set; } = GameStatus.WaitingForPlayers;
        public List<Player> Players { get; } = new List<Player>();
        public int QuizId { get; set; }
        public List<QuestionWithAnswers> Questions { get; set; } = new();
        public int CurrentQuestionIndex { get; set; } = -1;
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public bool HostJoined { get; set; }
        public string HostId => Players.FirstOrDefault(p => p.IsHost)?.PlayerId;
        public bool HostReady { get; set; }
        public int GuestsReady { get; set; }
        public List<string> CompletedPlayers { get; } = new List<string>();
        public bool IsTimeLimitEnabled { get; set; }
        public int TimeLimitPerQuestion { get; set; } = 30;
        public GameMode GameMode { get; set; } = GameMode.SingleChoice;
        public int NumberOfQuestions { get; set; } = 10;
    }

    public enum GameStatus
    {
        WaitingForPlayers,
        InProgress,
        Completed
    }

    public enum GameMode
    {
        SingleChoice,
        MultipleChoice
    }
}
