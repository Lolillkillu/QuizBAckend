using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Models;

namespace QuizzWebApp.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
            
        }


        public DbSet<QuizzModel> Quizzes { get; set; }
        public DbSet<QuestionModel> Questions { get; set; }
        public DbSet<AnswerModel> Answers { get; set; }
        public DbSet<UserModel> Users { get; set; }
        public DbSet<ScienceModel> Sciences { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<QuizzModel>()
                .HasOne(q => q.Science)
                .WithMany()
                .HasForeignKey(q => q.ScienceId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuestionModel>()
                .HasOne(q => q.Quizz)
                .WithMany(q => q.Questions)
                .HasForeignKey(q => q.QuizzId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnswerModel>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserModel>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<UserModel>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }


        public async Task<List<QuestionWithAnswers>> GetRandomQuestions(int quizzId)
        {
            //Sprawdzenie czy quiz istnieje
            if (!await Quizzes.AnyAsync(q => q.QuizzId == quizzId))
            {
                throw new InvalidOperationException($"Quiz o ID {quizzId} nie istnieje");
            }

            var questions = await Questions
                .Where(q => q.QuizzId == quizzId)
                .Include(q => q.Answers)
                .AsNoTracking()
                .ToListAsync();

            if (!questions.Any())
            {
                throw new InvalidOperationException($"Quiz o ID {quizzId} nie ma pytań");
            }

            var validQuestions = questions
                .Where(q => q.Answers != null &&
                           q.Answers.Any(a => a.IsCorrect) &&
                           q.Answers.Count(a => !a.IsCorrect) >= 3)
                .ToList();

            if (validQuestions.Count < 10)
            {
                throw new InvalidOperationException(
                    $"Wymagane 10 pytań. Dostępne: {validQuestions.Count}");
            }

            var selectedQuestions = validQuestions
                .OrderBy(_ => Guid.NewGuid())
                .Take(10)
                .ToList();

            var random = new Random();
            var result = new List<QuestionWithAnswers>();

            foreach (var question in selectedQuestions)
            {
                var correctAnswers = question.Answers!.Where(a => a.IsCorrect).ToList();
                var correctAnswer = correctAnswers[random.Next(correctAnswers.Count)];

                var incorrectAnswers = question.Answers!
                    .Where(a => !a.IsCorrect)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3)
                    .ToList();

                var allAnswers = new List<AnswerModel> { correctAnswer };
                allAnswers.AddRange(incorrectAnswers);

                result.Add(new QuestionWithAnswers
                {
                    QuestionId = question.QuestionId,
                    QuestionText = question.Question,
                    Answers = allAnswers
                        .OrderBy(_ => Guid.NewGuid())
                        .Select(a => new AnswerDto
                        {
                            AnswerId = a.AnswerId,
                            AnswerText = a.Answer,
                            IsCorrect = a.IsCorrect
                        })
                        .ToList()
                });
            }

            return result;
        }

        public async Task<List<QuestionWithAnswers>> GetRandomMultiQuestions(int quizzId, int numberOfQuestions = 10, int answersPerQuestion = 4)
        {
            if (numberOfQuestions <= 0 || answersPerQuestion <= 0)
                throw new ArgumentException("Liczba pytań i odpowiedzi powinna być większa od zera");

            if (!await Quizzes.AnyAsync(q => q.QuizzId == quizzId))
                throw new InvalidOperationException($"Quiz {quizzId} nie istnieje");

            var validQuestionIds = await Questions
                .Where(q => q.QuizzId == quizzId)
                .Select(q => new
                {
                    q.QuestionId,
                    CorrectCount = q.Answers.Count(a => a.IsCorrect),
                    IncorrectCount = q.Answers.Count(a => !a.IsCorrect),
                    TotalAnswers = q.Answers.Count
                })
                .Where(x =>
                    x.CorrectCount >= 1 &&
                    x.TotalAnswers >= answersPerQuestion &&
                    x.IncorrectCount >= (answersPerQuestion <= x.CorrectCount ? 0 : answersPerQuestion - x.CorrectCount))
                .Select(x => x.QuestionId)
                .ToListAsync();

            if (validQuestionIds.Count < numberOfQuestions)
                throw new InvalidOperationException($"Wymagane {numberOfQuestions} pytań. Dostępne: {validQuestionIds.Count}");

            var selectedQuestionIds = validQuestionIds
                .OrderBy(x => Guid.NewGuid())
                .Take(numberOfQuestions)
                .ToList();

            var selectedQuestions = await Questions
                .Where(q => selectedQuestionIds.Contains(q.QuestionId))
                .Include(q => q.Answers)
                .AsNoTracking()
                .ToListAsync();

            var random = new Random();
            var result = new List<QuestionWithAnswers>();

            foreach (var question in selectedQuestions)
            {
                var correctAnswers = question.Answers.Where(a => a.IsCorrect).ToList();
                var incorrectAnswers = question.Answers.Where(a => !a.IsCorrect).ToList();

                int totalCorrect = correctAnswers.Count;
                int totalIncorrect = incorrectAnswers.Count;

                int maxPossibleCorrect = Math.Min(answersPerQuestion, totalCorrect);
                int minRequiredCorrect = Math.Max(1, answersPerQuestion - totalIncorrect);
                minRequiredCorrect = Math.Min(minRequiredCorrect, maxPossibleCorrect);

                int correctToSelect = random.Next(minRequiredCorrect, maxPossibleCorrect + 1);

                var selectedCorrect = correctAnswers
                    .OrderBy(a => Guid.NewGuid())
                    .Take(correctToSelect)
                    .ToList();

                var selectedIncorrect = incorrectAnswers
                    .OrderBy(a => Guid.NewGuid())
                    .Take(answersPerQuestion - correctToSelect)
                    .ToList();

                var allAnswers = selectedCorrect.Concat(selectedIncorrect)
                    .OrderBy(a => Guid.NewGuid())
                    .ToList();

                result.Add(new QuestionWithAnswers
                {
                    QuestionId = question.QuestionId,
                    QuestionText = question.Question,
                    Answers = allAnswers.Select(a => new AnswerDto
                    {
                        AnswerId = a.AnswerId,
                        AnswerText = a.Answer,
                        IsCorrect = a.IsCorrect
                    }).ToList()
                });
            }

            return result;
        }
    }
}
