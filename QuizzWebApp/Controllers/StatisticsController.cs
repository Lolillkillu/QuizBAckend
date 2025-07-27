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
    }
}