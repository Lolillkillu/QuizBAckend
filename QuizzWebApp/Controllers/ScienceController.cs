using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizzWebApp.Data;
using QuizzWebApp.Models;

namespace QuizzWebApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScienceController : ControllerBase
    {
        private readonly DataContext _context;

        public ScienceController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ScienceModel>>> GetSciences()
        {
            return await _context.Sciences.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<ScienceModel>> AddScience(ScienceModel science)
        {
            if (string.IsNullOrWhiteSpace(science.ScienceName))
                return BadRequest("Nazwa dziedziny jest wymagana");

            _context.Sciences.Add(science);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetSciences), science);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteScience(int id)
        {
            var science = await _context.Sciences.FindAsync(id);
            if (science == null) return NotFound();

            if (await _context.ScienceStatistics.AnyAsync(ss => ss.ScienceId == id))
                return Conflict("Nie można usunąć dziedziny używanej w statystykach");

            var quizzes = await _context.Quizzes
                .Where(q => q.ScienceId == id)
                .ToListAsync();

            quizzes.ForEach(q => q.ScienceId = null);
            _context.Sciences.Remove(science);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
