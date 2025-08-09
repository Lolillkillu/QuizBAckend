using QuizzWebApp.Models;
using QuizzWebApp.Services;
using System.Reflection;

namespace QuizzWebApp.Tests.Services
{
    public class GameManagerTest : IDisposable
    {
        private readonly GameManager _sut;

        public GameManagerTest()
        {
            GameManager.DisableCleanupTimer = true;
            _sut = GameManager.Instance;

            var gamesField = typeof(GameManager)
                .GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, GameSession>)
                       gamesField.GetValue(_sut)!;
            dict.Clear();
        }

        public void Dispose() { }

        [Fact]
        public void CreateGame_ShouldReturnNewGameSession()
        {
            var game = _sut.CreateGame(123);
            Assert.NotNull(game);
            Assert.Equal(123, game.QuizId);
            Assert.NotEmpty(game.GameId);
        }

        [Fact]
        public void CreateGame_ShouldAddGameToInternalDictionary()
        {
            var game = _sut.CreateGame(1);
            var fetched = _sut.GetGame(game.GameId);
            Assert.Equal(game.GameId, fetched!.GameId);
        }


        [Fact]
        public void GetGame_ReturnsNull_WhenGameDoesNotExist()
        {
            Assert.Null(_sut.GetGame("non-existing"));
        }

        [Fact]
        public void GetGame_ReturnsGame_WhenExists()
        {
            var game = _sut.CreateGame(55);
            var fetched = _sut.GetGame(game.GameId);
            Assert.Equal(game.GameId, fetched!.GameId);
        }


        [Fact]
        public void JoinGame_ReturnsFalse_WhenGameNotFound()
        {
            Assert.False(_sut.JoinGame("xyz", "xxx", "Adam", false));
        }

        [Fact]
        public void JoinGame_ReturnsFalse_WhenGameStatusIsNotWaiting()
        {
            var game = _sut.CreateGame(1);
            game.Status = GameStatus.InProgress;

            Assert.False(_sut.JoinGame(game.GameId, "xxx", "Bob", false));
        }

        [Fact]
        public void JoinGame_ReturnsFalse_WhenHostAlreadyExists()
        {
            var game = _sut.CreateGame(1);
            _sut.JoinGame(game.GameId, "host1", "Adam", true);

            Assert.False(_sut.JoinGame(game.GameId, "host2", "Bob", true));
            Assert.Single(game.Players.Where(p => p.IsHost));
        }

        [Fact]
        public void JoinGame_ReturnsFalse_WhenTwoNonHostPlayersAlreadyJoined()
        {
            var game = _sut.CreateGame(1);
            _sut.JoinGame(game.GameId, "xxx", "Adam", false);
            _sut.JoinGame(game.GameId, "xxx", "Bob", false);

            Assert.False(_sut.JoinGame(game.GameId, "xxx", "Bartek", false));
            Assert.Equal(2, game.Players.Count);
        }

        [Fact]
        public void JoinGame_ReturnsTrue_AndAddsHost()
        {
            var game = _sut.CreateGame(1);

            var ok = _sut.JoinGame(game.GameId, "host", "Adam", true);

            Assert.True(ok);
            Assert.Single(game.Players);
            Assert.Equal("host", game.Players.Single().ConnectionId);
            Assert.True(game.Players.Single().IsHost);
        }

        [Fact]
        public void JoinGame_ReturnsTrue_AndAddsSecondPlayer()
        {
            var game = _sut.CreateGame(1);
            _sut.JoinGame(game.GameId, "hostConn", "Adam", true);

            var ok = _sut.JoinGame(game.GameId, "conn2", "Bob", false);

            Assert.True(ok);
            Assert.Equal(2, game.Players.Count);
            Assert.Contains(game.Players, p => p.Name == "Bob" && !p.IsHost);
        }

        [Fact]
        public void JoinGame_IsThreadSafe()
        {
            const int threads = 20;
            var game = _sut.CreateGame(1);
            _sut.JoinGame(game.GameId, "host", "Host", true);

            int successes = 0;

            Parallel.For(0, threads, i =>
            {
                if (_sut.JoinGame(game.GameId, $"xxx{i}", $"Player{i}", false))
                    Interlocked.Increment(ref successes);
            });

            Assert.True(successes <= 2);
        }

        [Fact]
        public void CleanupOldGames_RemovesGamesOlderThan30Minutes()
        {
            var oldGame = (GameSession)System.Runtime.Serialization.FormatterServices
                            .GetUninitializedObject(typeof(GameSession));

            typeof(GameSession).GetField("_gameId",
                        BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(oldGame, Guid.NewGuid().ToString());

            if (string.IsNullOrEmpty(oldGame.GameId))
            {
                typeof(GameSession).GetField("<GameId>k__BackingField",
                            BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(oldGame, Guid.NewGuid().ToString());
            }

            oldGame.QuizId = 1;

            typeof(GameSession).GetField("<CreatedAt>k__BackingField",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(oldGame, DateTime.UtcNow.AddHours(-1));

            var gamesField = typeof(GameManager)
                .GetField("_games", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, GameSession>)
                       gamesField.GetValue(_sut)!;
            dict.TryAdd(oldGame.GameId, oldGame);

            var freshGame = _sut.CreateGame(2);

            typeof(GameManager)
                .GetMethod("CleanupOldGames", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_sut, null);

            Assert.Null(_sut.GetGame(oldGame.GameId));
            Assert.NotNull(_sut.GetGame(freshGame.GameId));
        }
    }
}