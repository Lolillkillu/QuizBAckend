using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Controllers;
using QuizzWebApp.Data;
using QuizzWebApp.Models;

namespace QuizzWebApp.Tests.Controllers
{
    public class QuizzControllerTests
    {
        private readonly DataContext _context;
        private readonly QuizzController _controller;

        public QuizzControllerTests()
        {
            var options = new DbContextOptionsBuilder<DataContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new DataContext(options);
            _controller = new QuizzController(_context);

            SeedData();
        }

        private void SeedData()
        {
            var quiz = new QuizzModel
            {
                QuizzId = 1,
                Title = "Test Quiz",
                Description = "Test Desc",
                Author = "Tester",
                Questions = new List<QuestionModel>
                {
                    new QuestionModel
                    {
                        QuestionId = 1,
                        Question = "What is 2+2?",
                        Answers = new List<AnswerModel>
                        {
                            new AnswerModel { AnswerId = 1, Answer = "4", IsCorrect = true },
                            new AnswerModel { AnswerId = 2, Answer = "3", IsCorrect = false },
                            new AnswerModel { AnswerId = 3, Answer = "2", IsCorrect = false },
                            new AnswerModel { AnswerId = 4, Answer = "1", IsCorrect = false }
                        }
                    }
                }
            };

            _context.Quizzes.Add(quiz);
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetQuizzesAsync_ReturnsAllQuizzes()
        {
            var result = await _controller.GetQuizzesAsync();

            result.Value.Should().HaveCount(1);
            result.Value.First().Title.Should().Be("Test Quiz");
        }

        [Fact]
        public void GetQuizz_ReturnsNotFound_WhenQuizDoesNotExist()
        {
            var result = _controller.GetQuizz(999);

            result.Result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public void CreateQuizz_ReturnsBadRequest_WhenQuestionsAreProvided()
        {
            var invalidQuiz = new QuizzModel
            {
                Title = "Title",
                Author = "TestAuthor",
                Questions = new List<QuestionModel> { new QuestionModel() }
            };

            var result = _controller.CreateQuizz(invalidQuiz);

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public void CreateQuizz_ReturnsCreatedAtAction_WhenValid()
        {
            var validQuiz = new QuizzModel { Title = "New Quiz", Description = "hihih", Author = "Me" };

            var result = _controller.CreateQuizz(validQuiz);

            result.Result.Should().BeOfType<CreatedAtActionResult>();
            _context.Quizzes.Should().Contain(q => q.Title == "New Quiz");
        }

        [Fact]
        public async Task UpdateQuizz_ReturnsBadRequest_WhenIdsMismatch()
        {
            var quiz = new QuizzModel { QuizzId = 2, Title = "Some Title", Author = "Some Author" };

            var result = await _controller.UpdateQuizz(1, quiz);

            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task DeleteQuizz_ReturnsNotFound_WhenQuizDoesNotExist()
        {
            var result = _controller.DeleteQuizz(999);

            result.Should().BeOfType<NotFoundResult>();
        }

        [Fact]
        public async Task SubmitAnswer_ReturnsBadRequest_WhenInvalidInput()
        {
            var result = _controller.SubmitAnswer(new UserAnswer { AnswerId = 0, QuestionId = 0 });

            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task AssignScienceToQuiz_ReturnsNotFound_WhenQuizDoesNotExist()
        {
            var result = await _controller.AssignScienceToQuiz(999, 1);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task RemoveScienceFromQuiz_ReturnsNotFound_WhenQuizDoesNotExist()
        {
            var result = await _controller.RemoveScienceFromQuiz(999);

            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task SubmitGameResults_ReturnsBadRequest_WhenUsernameIsEmpty()
        {
            var result = await _controller.SubmitGameResults(new SubmitGameResult
            {
                Username = "",
                QuizId = 1,
                TotalQuestions = 5,
                CorrectAnswers = 3
            });

            result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}