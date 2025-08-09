using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuizzWebApp.Controllers;
using QuizzWebApp.Data;
using QuizzWebApp.Models;
using QuizzWebApp.Services;
using System.Reflection;

namespace QuizzWebApp.Tests
{
    public class GameHubTestsFixture : IDisposable
    {
        public DataContext Context { get; }

        public GameHubTestsFixture()
        {
            var opts = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new DataContext(opts);

            Context.Quizzes.Add(new QuizzModel
            {
                QuizzId = 1,
                Title = "Test Quiz",
                Author = "TestAuthor"
            });
            Context.SaveChanges();

            GameManager.ResetForTests();
            GameManager.DisableCleanupTimer = true;
        }

        public void Dispose()
        {
            Context.Dispose();
            GameManager.ResetForTests();
        }
    }

    public class TestableGameHub : GameHub
    {
        public Mock<IHubCallerClients> MockClients { get; } = new();
        public Mock<IGroupManager> MockGroups { get; } = new();
        public Mock<HubCallerContext> MockContext { get; } = new();

        public TestableGameHub(DataContext ctx) : base(ctx)
        {
            Clients = MockClients.Object;
            Groups = MockGroups.Object;
            Context = MockContext.Object;
        }
    }

    public class GameHubTests : IClassFixture<GameHubTestsFixture>
    {
        private readonly GameHubTestsFixture _fixture;
        public GameHubTests(GameHubTestsFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task CreateGame_ReturnsNonEmptyGameId()
        {
            var hub = new TestableGameHub(_fixture.Context);
            var id = await hub.CreateGame(1);
            Assert.NotNull(id);
            Assert.NotEmpty(id);
        }

        [Fact]
        public async Task JoinGame_ReturnsPlayerIdAndNotifiesGroup()
        {
            var hub = new TestableGameHub(_fixture.Context);
            hub.MockContext.Setup(c => c.ConnectionId).Returns("xxx");

            var gameId = await hub.CreateGame(1);

            var groupClient = new Mock<IClientProxy>();
            hub.MockGroups
               .Setup(g => g.AddToGroupAsync("xxx", gameId, default))
               .Returns(Task.CompletedTask);
            hub.MockClients.Setup(c => c.Group(gameId)).Returns(groupClient.Object);

            var pid = await hub.JoinGame(gameId, "Alice", false);

            Assert.NotNull(pid);
            groupClient.Verify(
                c => c.SendCoreAsync(
                    "PlayerJoined",
                    It.Is<object[]>(o => ((IEnumerable<dynamic>)o[0]).Count() == 1),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SubmitAnswer_IncreasesScoreWhenCorrect()
        {
            var hub = new TestableGameHub(_fixture.Context);
            hub.MockContext.Setup(c => c.ConnectionId).Returns("xxx");

            var gameId = await hub.CreateGame(1);

            var groupClient = new Mock<IClientProxy>();
            hub.MockGroups
               .Setup(g => g.AddToGroupAsync("xxx", gameId, default))
               .Returns(Task.CompletedTask);
            hub.MockClients.Setup(c => c.Group(gameId)).Returns(groupClient.Object);

            await hub.JoinGame(gameId, "Adam", false);

            var game = GameManager.Instance.GetGame(gameId);
            game.Status = GameStatus.InProgress;
            game.Questions = new List<QuestionWithAnswers>
            {
                new QuestionWithAnswers
                {
                    QuestionId = 1,
                    QuestionText = "2+2?",
                    Answers = new List<AnswerDto>
                    {
                        new AnswerDto { AnswerId = 1, AnswerText = "4", IsCorrect = true },
                        new AnswerDto { AnswerId = 2, AnswerText = "5", IsCorrect = false }
                    }
                }
            };
            var player = game.Players.Single();
            player.CurrentQuestionIndex = 0;

            var caller = new Mock<ISingleClientProxy>();
            hub.MockClients.Setup(c => c.Caller).Returns(caller.Object);

            await hub.SubmitAnswer(gameId, 1, 1);

            Assert.Equal(1, player.Score);
            caller.Verify(
                c => c.SendCoreAsync(
                    "AnswerProcessed",
                    It.Is<object[]>(o => (bool)o[1] == true),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task EndGame_SavesStatsToDb()
        {
            var hub = new TestableGameHub(_fixture.Context);
            hub.MockContext.Setup(c => c.ConnectionId).Returns("xxx");

            _fixture.Context.Users.Add(new UserModel
            {
                UserId = 1,
                Username = "Adam",
                Password = "Password",
                Email = "adam@example.com",
                IsAdmin = false
            });
            await _fixture.Context.SaveChangesAsync();

            var gameId = await hub.CreateGame(1);

            var groupClient = new Mock<ISingleClientProxy>();
            hub.MockGroups
               .Setup(g => g.AddToGroupAsync("xxx", gameId, default))
               .Returns(Task.CompletedTask);
            hub.MockClients.Setup(c => c.Group(gameId)).Returns(groupClient.Object);

            await hub.JoinGame(gameId, "Adam", false);

            var game = GameManager.Instance.GetGame(gameId);
            game.Status = GameStatus.InProgress;
            game.Questions = new List<QuestionWithAnswers>
            {
                new QuestionWithAnswers
                {
                    QuestionId = 1,
                    QuestionText = "QuestionText?",
                    Answers = new List<AnswerDto>
                    {
                        new AnswerDto { AnswerId = 1, AnswerText = "A", IsCorrect = true }
                    }
                }
            };
            var player = game.Players.Single();
            player.Score = 1;
            player.HasCompleted = true;

            var endMethod = typeof(GameHub)
                .GetMethod("EndGame", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(endMethod);
            await (Task)endMethod.Invoke(hub, new object[] { gameId });

            var stat = await _fixture.Context.QuizStatistics
                .FirstOrDefaultAsync(q => q.UserId == 1);
            Assert.NotNull(stat);
            Assert.Equal(1, stat.CorrectAnswers);
        }
    }
}