using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Data;
using QuizzWebApp.Models;

namespace QuizzWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly DataContext _context;

        public StatisticsController(DataContext context)
        {
            _context = context;
        }

        [HttpGet("quiz/{quizId}")]
        public async Task<IActionResult> GetQuizStatistics(int quizId)
        {
            var stats = await _context.QuizStatistics
                .Where(q => q.QuizzId == quizId)
                .Include(q => q.User)
                .OrderByDescending(q => q.DateCompleted)
                .ToListAsync();

            return Ok(stats.Select(s => new
            {
                s.User.Username,
                s.TotalQuestions,
                s.CorrectAnswers,
                s.ScorePercentage,
                s.DateCompleted
            }));
        }

        [HttpGet("science/{scienceId}")]
        public async Task<IActionResult> GetScienceStatistics(int scienceId)
        {
            var stats = await _context.ScienceStatistics
                .Where(s => s.ScienceId == scienceId)
                .Include(s => s.User)
                .ToListAsync();

            return Ok(stats.Select(s => new
            {
                s.User.Username,
                s.TotalQuizzesTaken,
                s.TotalQuestionsAnswered,
                s.TotalCorrectAnswers,
                s.OverallAccuracy
            }));
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserStatistics(int userId)
        {
            var quizStats = await _context.QuizStatistics
                .Where(q => q.UserId == userId)
                .Include(q => q.Quiz)
                .OrderByDescending(q => q.DateCompleted)
                .ToListAsync();

            var scienceStats = await _context.ScienceStatistics
                .Where(s => s.UserId == userId)
                .Include(s => s.Science)
                .ToListAsync();

            return Ok(new
            {
                QuizHistory = quizStats.Select(q => new
                {
                    QuizId = q.QuizzId,
                    QuizTitle = q.Quiz.Title,
                    q.TotalQuestions,
                    q.CorrectAnswers,
                    q.ScorePercentage,
                    q.DateCompleted
                }),
                ScienceSummary = scienceStats.Select(s => new
                {
                    ScienceName = s.Science.ScienceName,
                    s.TotalQuizzesTaken,
                    s.OverallAccuracy
                })
            });
        }

        [HttpDelete("quiz/{quizId}/user/{userId}")]
        public async Task<IActionResult> DeleteQuizStatisticsForUser(int quizId, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var quizStats = await _context.QuizStatistics
                    .Where(qs => qs.QuizzId == quizId && qs.UserId == userId)
                    .ToListAsync();

                if (!quizStats.Any())
                {
                    return NotFound("Nie znaleziono statystyk dla podanego quizu i użytkownika");
                }

                var quiz = await _context.Quizzes
                    .Include(q => q.Science)
                    .FirstOrDefaultAsync(q => q.QuizzId == quizId);

                if (quiz == null)
                {
                    return NotFound("Nie znaleziono quizu");
                }

                var scienceId = quiz.ScienceId;

                int totalQuestionsToSubtract = quizStats.Sum(qs => qs.TotalQuestions);
                int correctAnswersToSubtract = quizStats.Sum(qs => qs.CorrectAnswers);
                int quizzesTakenToSubtract = quizStats.Count;

                _context.QuizStatistics.RemoveRange(quizStats);

                var scienceStats = await _context.ScienceStatistics
                    .FirstOrDefaultAsync(ss => ss.UserId == userId && ss.ScienceId == scienceId);

                if (scienceStats != null)
                {
                    scienceStats.TotalQuizzesTaken -= quizzesTakenToSubtract;
                    scienceStats.TotalQuestionsAnswered -= totalQuestionsToSubtract;
                    scienceStats.TotalCorrectAnswers -= correctAnswersToSubtract;

                    scienceStats.TotalQuizzesTaken = Math.Max(0, scienceStats.TotalQuizzesTaken);
                    scienceStats.TotalQuestionsAnswered = Math.Max(0, scienceStats.TotalQuestionsAnswered);
                    scienceStats.TotalCorrectAnswers = Math.Max(0, scienceStats.TotalCorrectAnswers);

                    if (scienceStats.TotalQuizzesTaken == 0 || scienceStats.TotalQuestionsAnswered == 0)
                    {
                        _context.ScienceStatistics.Remove(scienceStats);
                    }
                    else
                    {
                        _context.ScienceStatistics.Update(scienceStats);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    Message = "Statystyki zostały pomyslnie usunięte",
                    RemovedQuizzes = quizzesTakenToSubtract,
                    RemovedQuestions = totalQuestionsToSubtract,
                    RemovedCorrectAnswers = correctAnswersToSubtract,
                    ScienceStatisticsDeleted = scienceStats == null ||
                        scienceStats.TotalQuizzesTaken == 0 ||
                        scienceStats.TotalQuestionsAnswered == 0
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Wystąpił błąd podczas usuwania statystyk: {ex.Message}");
            }
        }
    }
}