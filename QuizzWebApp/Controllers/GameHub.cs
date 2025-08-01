﻿using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Data;
using QuizzWebApp.Models;
using QuizzWebApp.Services;

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

        public async Task SetTimeSettings(string gameId, bool isTimeLimitEnabled, int timeLimitPerQuestion)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId && p.IsHost);
            if (player == null) return;

            game.IsTimeLimitEnabled = isTimeLimitEnabled;
            game.TimeLimitPerQuestion = timeLimitPerQuestion;
        }

        public async Task SetGameMode(string gameId, GameMode mode)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId && p.IsHost);
            if (player == null) return;

            game.GameMode = mode;

            await Clients.Group(gameId).SendAsync("GameModeUpdated", mode);
        }

        public async Task SubmitAnswer(string gameId, int questionId, int? answerId)
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
            bool isCorrect = false;
            string answerText = "Brak odpowiedzi";
            List<string> answerTexts = new List<string>();
            List<int> answerIds = new List<int>();

            if (answerId.HasValue)
            {
                var selectedAnswer = currentQuestion.Answers.FirstOrDefault(a => a.AnswerId == answerId.Value);
                if (selectedAnswer == null) return;

                isCorrect = selectedAnswer.IsCorrect;
                answerText = selectedAnswer.AnswerText;
                answerTexts.Add(answerText);
                answerIds.Add(answerId.Value);
            }

            lock (player)
            {
                if (isCorrect) player.Score++;

                player.Answers.Add(new PlayerAnswer
                {
                    QuestionId = questionId,
                    QuestionText = currentQuestion.QuestionText,
                    AnswerIds = answerIds,
                    AnswerTexts = answerTexts,
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
                    Answers = nextQuestion.Answers.Select(a => new { a.AnswerId, a.AnswerText }),
                    IsTimeLimitEnabled = game.IsTimeLimitEnabled,
                    TimeLimitPerQuestion = game.TimeLimitPerQuestion,
                    IsMultiChoice = (game.GameMode == GameMode.MultipleChoice)
                });
            }
        }

        public async Task SubmitMultiAnswer(string gameId, int questionId, List<int> answerIds)
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
            bool isCorrect = false;
            List<string> answerTexts = new List<string>();

            if (answerIds != null && answerIds.Any())
            {
                var selectedAnswers = currentQuestion.Answers
                    .Where(a => answerIds.Contains(a.AnswerId))
                    .ToList();

                answerTexts = selectedAnswers.Select(a => a.AnswerText).ToList();

                var correctAnswerIds = currentQuestion.Answers
                    .Where(a => a.IsCorrect)
                    .Select(a => a.AnswerId)
                    .ToList();

                isCorrect = correctAnswerIds.Count == answerIds.Count &&
                            correctAnswerIds.All(id => answerIds.Contains(id));
            }

            lock (player)
            {
                if (isCorrect) player.Score++;

                player.Answers.Add(new PlayerAnswer
                {
                    QuestionId = questionId,
                    QuestionText = currentQuestion.QuestionText,
                    AnswerIds = answerIds ?? new List<int>(),
                    AnswerTexts = answerTexts,
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
                    Answers = nextQuestion.Answers.Select(a => new { a.AnswerId, a.AnswerText }),
                    IsTimeLimitEnabled = game.IsTimeLimitEnabled,
                    TimeLimitPerQuestion = game.TimeLimitPerQuestion,
                    IsMultiChoice = (game.GameMode == GameMode.MultipleChoice)
                });
            }
        }

        private async Task StartGame(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            game.Status = GameStatus.InProgress;

            if (game.GameMode == GameMode.SingleChoice)
            {
                game.Questions = await _context.GetRandomQuestions(game.QuizId, game.NumberOfQuestions);
            }
            else
            {
                game.Questions = await _context.GetRandomMultiQuestions(game.QuizId, game.NumberOfQuestions, game.AnswersPerQuestion);
            }

            foreach (var player in game.Players)
            {
                player.CurrentQuestionIndex = 0;
                player.HasCompleted = false;
                player.Score = 0;
                player.Answers.Clear();

                var question = game.Questions[0];
                await Clients.Client(player.ConnectionId).SendAsync("NextQuestion", new
                {
                    question.QuestionId,
                    question.QuestionText,
                    Answers = question.Answers.Select(a => new { a.AnswerId, a.AnswerText }),
                    IsTimeLimitEnabled = game.IsTimeLimitEnabled,
                    TimeLimitPerQuestion = game.TimeLimitPerQuestion,
                    IsMultiChoice = (game.GameMode == GameMode.MultipleChoice),
                    TotalQuestions = game.NumberOfQuestions
                });
            }
        }

        private async Task EndGame(string gameId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            game.Status = GameStatus.Completed;
            await SaveGameResults(game);

            var correctAnswersByQuestion = game.Questions.ToDictionary(
                q => q.QuestionId,
                q => q.Answers.Where(a => a.IsCorrect).ToList()
            );

            await Clients.Group(gameId).SendAsync("GameCompleted",
                new
                {
                    players = game.Players.Select(p => new
                    {
                        playerId = p.PlayerId,
                        playerName = p.Name,
                        score = p.Score,
                        answers = p.Answers.Select(a => new
                        {
                            questionId = a.QuestionId,
                            questionText = a.QuestionText,
                            answerIds = a.AnswerIds,
                            answerTexts = a.AnswerTexts,
                            isCorrect = a.IsCorrect,
                            correctAnswerIds = correctAnswersByQuestion[a.QuestionId]
                                .Select(ca => ca.AnswerId).ToList(),
                            correctAnswerTexts = correctAnswersByQuestion[a.QuestionId]
                                .Select(ca => ca.AnswerText).ToList()
                        }).ToList()
                    }).ToList(),
                    questions = game.Questions.Select(q => new
                    {
                        questionId = q.QuestionId,
                        questionText = q.QuestionText,
                        correctAnswers = q.Answers
                            .Where(a => a.IsCorrect)
                            .Select(a => new {
                                answerId = a.AnswerId,
                                answerText = a.AnswerText
                            }).ToList()
                    }).ToList()
                });
        }

        public async Task SetQuizIdForGame(string gameId, int quizId)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game != null)
            {
                game.QuizId = quizId;
            }
        }

        public async Task SetNumberOfQuestions(string gameId, int numberOfQuestions)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            var player = game.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId && p.IsHost);
            if (player == null) return;

            game.NumberOfQuestions = Math.Clamp(numberOfQuestions, 5, 20);
        }

        public async Task SetAnswersPerQuestion(string gameId, int answersPerQuestion)
        {
            var game = GameManager.Instance.GetGame(gameId);
            if (game == null) return;

            var player = game.Players.FirstOrDefault(p =>
                p.ConnectionId == Context.ConnectionId && p.IsHost);

            if (player == null) return;

            game.AnswersPerQuestion = Math.Clamp(answersPerQuestion, 2, 6);
        }

        private async Task SaveGameResults(GameSession game)
        {
            if (game == null || !game.Players.Any()) return;

            var quiz = await _context.Quizzes
                .Include(q => q.Science)
                .FirstOrDefaultAsync(q => q.QuizzId == game.QuizId);

            if (quiz == null) return;

            foreach (var player in game.Players)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == player.Name);

                if (user == null)
                    continue;

                var totalQuestions = game.Questions.Count;
                var correctAnswers = player.Score;

                var quizStat = new QuizStatistics
                {
                    UserId = user.UserId,
                    QuizzId = game.QuizId,
                    TotalQuestions = totalQuestions,
                    CorrectAnswers = correctAnswers,
                    DateCompleted = DateTime.UtcNow
                };

                _context.QuizStatistics.Add(quizStat);

                if (quiz.ScienceId.HasValue)
                {
                    var scienceId = quiz.ScienceId.Value;
                    var scienceStat = await _context.ScienceStatistics
                        .FirstOrDefaultAsync(s => s.UserId == user.UserId && s.ScienceId == scienceId);

                    if (scienceStat == null)
                    {
                        scienceStat = new ScienceStatistics
                        {
                            UserId = user.UserId,
                            ScienceId = scienceId,
                            TotalQuizzesTaken = 1,
                            TotalQuestionsAnswered = totalQuestions,
                            TotalCorrectAnswers = correctAnswers
                        };
                        _context.ScienceStatistics.Add(scienceStat);
                    }
                    else
                    {
                        scienceStat.TotalQuizzesTaken++;
                        scienceStat.TotalQuestionsAnswered += totalQuestions;
                        scienceStat.TotalCorrectAnswers += correctAnswers;
                        _context.ScienceStatistics.Update(scienceStat);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<int> GetUserIdByUsernameAsync(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new UserModel
                {
                    Username = username,
                    Email = $"{username}@guest.local",
                    Password = "guest",
                    IsAdmin = false
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            return user.UserId;
        }
    }
}