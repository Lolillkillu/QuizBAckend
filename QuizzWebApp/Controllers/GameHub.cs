using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using QuizzWebApp.Data;
using QuizzWebApp.Models;
using QuizzWebApp.Services;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace QuizzWebApp.Controllers
{
    public class GameHub : Hub
    {
        private readonly DataContext _context;

        public GameHub(DataContext context)
        {
            _context = context;
        }

        public async Task<string> CreateGame(int quizId)
        {
            var game = GameManager.Instance.CreateGame(quizId);
            return game.GameId;
        }

        public async Task<string> JoinGame(string gameId, string playerName, bool isHost)
        {
            if (!GameManager.Instance.JoinGame(gameId, Context.ConnectionId, playerName, isHost))
                return null;

            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
            var game = GameManager.Instance.GetGame(gameId);

            await Clients.Group(gameId).SendAsync("PlayerJoined",
                game.Players.Select(p => new {
                    p.PlayerId,
                    p.Name,
                    p.IsHost,
                    p.IsReady,
                    p.HasCompleted,
                    p.Score
                }));

            return game.Players.Last().PlayerId;
        }

        public async Task PlayerReady(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            player.IsReady = true;

            await Clients.Group(gameId).SendAsync("PlayerReady", player.PlayerId);

            if (game.Players.Count >= 2 && game.Players.Count(p => p.IsReady) >= 2)
            {
                await StartGame(gameId);
            }
        }

        public async Task SubmitAnswer(string gameId, int questionId, int answerId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game?.Status != GameStatus.InProgress) return;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null) return;

            if (player.CurrentQuestionIndex >= game.Questions.Count ||
                game.Questions[player.CurrentQuestionIndex].QuestionId != questionId)
            {
                return;
            }

            var currentQuestion = game.Questions[player.CurrentQuestionIndex];
            var selectedAnswer = currentQuestion.Answers.FirstOrDefault(a => a.AnswerId == answerId);

            if (selectedAnswer == null) return;

            bool isCorrect = selectedAnswer.IsCorrect;

            lock (player)
            {
                if (isCorrect) player.Score++;

                player.Answers.Add(new PlayerAnswer
                {
                    QuestionId = questionId,
                    QuestionText = currentQuestion.QuestionText,
                    AnswerId = answerId,
                    AnswerText = selectedAnswer.AnswerText,
                    IsCorrect = isCorrect
                });

                player.CurrentQuestionIndex++;
            }

            await Clients.Caller.SendAsync("AnswerProcessed", player.PlayerId, isCorrect);

            if (player.CurrentQuestionIndex >= game.Questions.Count)
            {
                player.HasCompleted = true;
                await Clients.Group(gameId).SendAsync("PlayerCompleted", player.PlayerId);

                if (game.Players.All(p => p.HasCompleted))
                {
                    await EndGame(gameId);
                }
            }
            else
            {
                var nextQuestion = game.Questions[player.CurrentQuestionIndex];
                await Clients.Caller.SendAsync("NextQuestion", new
                {
                    nextQuestion.QuestionId,
                    nextQuestion.QuestionText,
                    Answers = nextQuestion.Answers.Select(a => new { a.AnswerId, a.AnswerText })
                });
            }
        }

        private async Task StartGame(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            game.Status = GameStatus.InProgress;
            game.Questions = await _context.GetRandomQuestions(game.QuizId);

            foreach (var player in game.Players)
            {
                player.CurrentQuestionIndex = 0;
                player.HasCompleted = false;

                var question = game.Questions[0];
                await Clients.Client(player.ConnectionId).SendAsync("NextQuestion", new
                {
                    question.QuestionId,
                    question.QuestionText,
                    Answers = question.Answers.Select(a => new { a.AnswerId, a.AnswerText })
                });
            }
        }

        private async Task SendNextQuestion(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null || game.CurrentQuestionIndex >= game.Questions.Count)
            {
                await EndGame(gameId);
                return;
            }

            var question = game.Questions[game.CurrentQuestionIndex];
            await Clients.Group(gameId).SendAsync("NextQuestion",
                new
                {
                    question.QuestionId,
                    question.QuestionText,
                    Answers = question.Answers.Select(a => new { a.AnswerId, a.AnswerText })
                });
        }

        private async Task EndGame(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            game.Status = GameStatus.Completed;
            await SaveGameResults(game);

            await Clients.Group(gameId).SendAsync("GameCompleted",
                game.Players.Select(p => new
                {
                    playerId = p.PlayerId,
                    playerName = p.Name,
                    score = p.Score,
                    answers = p.Answers.Select(a => new
                    {
                        questionId = a.QuestionId,
                        questionText = a.QuestionText,
                        answerId = a.AnswerId,
                        answerText = a.AnswerText,
                        isCorrect = a.IsCorrect
                    }).ToList()
                }).ToList());
        }

        public async Task SetQuizIdForGame(string gameId, int quizId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game != null)
            {
                game.QuizId = quizId;
            }
        }

        private async Task SaveGameResults(GameSession game)
        {
            //TODO zapis do bazy
            //przyszłe statystyki???
        }
    }
}