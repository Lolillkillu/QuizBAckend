using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Data;
using QuizzWebApp.Models;

namespace QuizzWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizzController : ControllerBase
    {
        private readonly DataContext _context;

        public QuizzController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<QuizzModel>>> GetQuizzesAsync()
        {
            var quizzes = await _context.Quizzes
                .Include(q => q.Questions!)
                .ThenInclude(q => q.Answers!)
                .ToListAsync();

            return quizzes;
        }

        [HttpGet("{id}")]
        public ActionResult<QuizzModel> GetQuizz(int id)
        {
            var quizz = _context.Quizzes
                                .Include(q => q.Questions!)
                                .ThenInclude(q => q.Answers!)
                                .FirstOrDefault(q => q.QuizzId == id);

            if (quizz == null)
            {
                return NotFound();
            }

            return quizz;
        }


        [HttpPost]
        public ActionResult<QuizzModel> CreateQuizz(QuizzModel quizz)
        {
            if (quizz.Questions != null && quizz.Questions.Any())
            {
                return BadRequest("Error");
            }

            _context.Quizzes.Add(quizz);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetQuizz), new { id = quizz.QuizzId }, quizz);
        }

        [HttpPut("{id}")]
        public IActionResult UpdateQuizz(int id, QuizzModel quizz)
        {
            if (id != quizz.QuizzId)
            {
                return BadRequest();
            }

            _context.Entry(quizz).State = EntityState.Modified;
            _context.SaveChanges();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteQuizz(int id)
        {
            var quizz = _context.Quizzes.Find(id);
            if (quizz == null)
            {
                return NotFound();
            }

            _context.Quizzes.Remove(quizz);
            _context.SaveChanges();

            return NoContent();
        }

        [HttpPost("{quizzId}/Question")]
        public ActionResult<QuestionModel> AddQuestionToQuizz(int quizzId, [FromBody] QuestionModel question)
        {
            var quizz = _context.Quizzes.Find(quizzId);
            if (quizz == null)
            {
                return NotFound();
            }

            question.QuizzId = quizzId;
            _context.Questions.Add(question);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetQuizz), new { id = quizzId }, question);
        }

        [HttpPut("Question/{id}")]
        public IActionResult UpdateQuestion(int id, QuestionModel question)
        {
            if (id != question.QuestionId)
            {
                return BadRequest();
            }

            _context.Entry(question).State = EntityState.Modified;
            _context.SaveChanges();

            return NoContent();
        }

        [HttpDelete("Question/{id}")]
        public IActionResult DeleteQuestion(int id)
        {
            var question = _context.Questions.Find(id);
            if (question == null)
            {
                return NotFound();
            }

            _context.Questions.Remove(question);
            _context.SaveChanges();

            return NoContent();
        }

        [HttpPost("Question/{questionId}/Answer")]
        public ActionResult<AnswerModel> AddAnswerToQuestion(int questionId, AnswerModel answer)
        {
            var question = _context.Questions.Find(questionId);
            if (question == null)
            {
                return NotFound();
            }

            answer.QuestionId = questionId;
            _context.Answers.Add(answer);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetQuizz), new { id = questionId }, answer);
        }

        [HttpPut("Answer/{id}")]
        public IActionResult UpdateAnswer(int id, AnswerModel answer)
        {
            if (id != answer.AnswerId)
            {
                return BadRequest();
            }

            _context.Entry(answer).State = EntityState.Modified;
            _context.SaveChanges();

            return NoContent();
        }

        [HttpDelete("Answer/{id}")]
        public IActionResult DeleteAnswer(int id)
        {
            var answer = _context.Answers.Find(id);
            if (answer == null)
            {
                return NotFound();
            }

            _context.Answers.Remove(answer);
            _context.SaveChanges();

            return NoContent();
        }

        [HttpGet("GetRandomQuestions/{quizzId}")]
        public ActionResult<List<QuestionWithAnswers>> GetRandomQuestions(int quizzId)
        {
            if (!_context.Quizzes.Any(q => q.QuizzId == quizzId))
            {
                return NotFound($"Quiz o ID {quizzId} nie istnieje w systemie");
            }

            var questions = _context.Questions
                .Where(q => q.QuizzId == quizzId)
                .Include(q => q.Answers)
                .AsNoTracking()
                .ToList();

            if (!questions.Any())
            {
                return NotFound($"Quiz o ID {quizzId} nie zawiera żadnych pytań");
            }

            var validQuestions = questions.Where(q =>
                q.Answers != null &&
                q.Answers.Any(a => a.IsCorrect) &&
                q.Answers.Count(a => !a.IsCorrect) >= 3)
                .ToList();

            if (validQuestions.Count < 10)
            {
                return NotFound($"Wymagane 10 pytań. Dostępnych jest tylko {validQuestions.Count} poprawnych pytań.");
            }

            var selectedQuestions = validQuestions
                .OrderBy(q => Guid.NewGuid())
                .Take(10)
                .ToList();

            var random = new Random();
            var result = new List<QuestionWithAnswers>();

            foreach (var question in selectedQuestions)
            {
                var correctAnswers = question.Answers?.Where(a => a.IsCorrect).ToList() ?? new List<AnswerModel>();
                var correctAnswer = correctAnswers[random.Next(correctAnswers.Count)];

                var incorrectAnswers = question.Answers?
                    .Where(a => !a.IsCorrect)
                    .OrderBy(a => Guid.NewGuid())
                    .Take(3)
                    .ToList() ?? new List<AnswerModel>();

                var allAnswers = new List<AnswerModel> { correctAnswer };
                allAnswers.AddRange(incorrectAnswers);

                result.Add(new QuestionWithAnswers
                {
                    QuestionId = question.QuestionId,
                    QuestionText = question.Question,
                    Answers = allAnswers
                        .OrderBy(a => Guid.NewGuid())
                        .Select(a => new AnswerDto
                        {
                            AnswerId = a.AnswerId,
                            AnswerText = a.Answer,
                            IsCorrect = a.IsCorrect
                        })
                        .ToList()
                });
            }

            return Ok(result);
        }

        [HttpPost("SubmitAnswer")]
        public ActionResult<AnswerResult> SubmitAnswer([FromBody] UserAnswer userAnswer)
        {
            if (userAnswer == null || userAnswer.AnswerId <= 0 || userAnswer.QuestionId <= 0)
            {
                return BadRequest("Nieprawidłowe dane wejściowe");
            }

            var submittedAnswer = _context.Answers
                .FirstOrDefault(a => a.AnswerId == userAnswer.AnswerId && a.QuestionId == userAnswer.QuestionId);

            if (submittedAnswer == null)
            {
                return BadRequest("Podana odpowiedź nie należy do tego pytania");
            }

            var correctAnswer = _context.Answers
                .FirstOrDefault(a => a.QuestionId == userAnswer.QuestionId && a.IsCorrect);

            if (correctAnswer == null)
            {
                return NotFound("nie znaleziono poprawnych odpowiedzi");
            }

            bool isCorrect = userAnswer.AnswerId == correctAnswer.AnswerId;

            return Ok(new AnswerResult
            {
                IsCorrect = isCorrect,
                CorrectAnswerId = correctAnswer.AnswerId,
                CorrectAnswerText = correctAnswer.Answer
            });
        }
    }
}
