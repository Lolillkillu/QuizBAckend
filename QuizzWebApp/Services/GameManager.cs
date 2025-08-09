using QuizzWebApp.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("QuizzWebApp.Tests")]

namespace QuizzWebApp.Services
{
    public class GameManager
    {
        internal static bool DisableCleanupTimer { get; set; }

        private static readonly Lazy<GameManager> _instance =
            new Lazy<GameManager>(() => new GameManager());

        public static GameManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, GameSession> _games =
            new ConcurrentDictionary<string, GameSession>();

        private readonly TimeSpan _gameTimeout = TimeSpan.FromMinutes(30);

        private GameManager()
        {
            if (!DisableCleanupTimer)
            {
                _ = new Timer(_ => CleanupOldGames(), null, 0, 300_000);
            }
        }

        public GameSession CreateGame(int quizId)
        {
            var game = new GameSession { QuizId = quizId };
            while (true)
            {
                if (_games.TryAdd(game.GameId, game))
                    return game;
            }
        }

        public bool JoinGame(string gameId, string connectionId, string playerName, bool isHost)
        {
            if (!_games.TryGetValue(gameId, out var game) ||
                game.Status != GameStatus.WaitingForPlayers)
                return false;

            if (isHost)
            {
                if (!string.IsNullOrEmpty(game.HostId))
                    return false;
            }
            else
            {
                if (game.Players.Count >= 2)
                    return false;
            }

            lock (game)
            {
                if (game.Status != GameStatus.WaitingForPlayers)
                    return false;

                game.Players.Add(new Player
                {
                    ConnectionId = connectionId,
                    PlayerId = Guid.NewGuid().ToString(),
                    Name = playerName,
                    IsHost = isHost,
                    IsReady = false
                });
            }
            return true;
        }

        public GameSession GetGame(string gameId) =>
            _games.TryGetValue(gameId, out var game) ? game : null;

        private void CleanupOldGames()
        {
            var cutoff = DateTime.UtcNow - _gameTimeout;
            var oldGames = _games.Where(g => g.Value.CreatedAt < cutoff).ToList();

            foreach (var game in oldGames)
                _games.TryRemove(game.Key, out _);
        }
    }
}