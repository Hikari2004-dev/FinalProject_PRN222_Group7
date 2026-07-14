using FinalProject_PRN222_Group7.BLL.Services;
using FinalProject_PRN222_Group7.DAL.Data;
using FinalProject_PRN222_Group7.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FinalProject_PRN222_Group7.Pages.Benchmark
{
    public class IndexModel : PageModel
    {
        private readonly IBenchmarkService _benchmarkService;
        private readonly UserManager<AppUser> _userManager;

        public IndexModel(IBenchmarkService benchmarkService, UserManager<AppUser> userManager)
        {
            _benchmarkService = benchmarkService;
            _userManager = userManager;
        }

        public IEnumerable<BenchmarkRun> Runs { get; set; } = new List<BenchmarkRun>();

        public async Task OnGetAsync()
        {
            Runs = await _benchmarkService.GetBenchmarkRunsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string name, string embeddingModel, int chunkSize, int chunkOverlap, int totalQuestions)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Auth/Login");

            // Mock calculations based on parameter ranges
            var random = new Random();
            double multiplier = chunkSize switch
            {
                < 300 => 0.9,
                <= 600 => 1.0,
                _ => 0.95
            };

            double baseAcc = 0.72 + (random.NextDouble() * 0.15);
            var run = new BenchmarkRun
            {
                Name = name,
                ChunkingStrategy = "RecursiveCharacterTextSplitter",
                ChunkSize = chunkSize,
                ChunkOverlap = chunkOverlap,
                EmbeddingModel = embeddingModel,
                TotalQuestions = totalQuestions,
                Faithfulness = Math.Min(1.0, (0.75 + (random.NextDouble() * 0.20)) * multiplier),
                AnswerRelevancy = Math.Min(1.0, (0.70 + (random.NextDouble() * 0.22)) * multiplier),
                ContextPrecision = Math.Min(1.0, (0.68 + (random.NextDouble() * 0.25)) * multiplier),
                ContextRecall = Math.Min(1.0, (0.65 + (random.NextDouble() * 0.28)) * multiplier),
                RunById = user.Id
            };

            run.OverallAccuracy = (run.Faithfulness + run.AnswerRelevancy + run.ContextPrecision + run.ContextRecall) / 4.0;

            await _benchmarkService.CreateBenchmarkRunAsync(run);

            return RedirectToPage();
        }
    }
}
