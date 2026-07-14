using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Pages.Benchmark
{
    public class DetailModel : PageModel
    {
        private readonly AppDbContext _context;

        public DetailModel(AppDbContext context)
        {
            _context = context;
        }

        public BenchmarkRun? Run { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null || id <= 0)
            {
                return NotFound();
            }

            Run = await _context.BenchmarkRuns.FirstOrDefaultAsync(r => r.Id == id.Value);

            if (Run == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
