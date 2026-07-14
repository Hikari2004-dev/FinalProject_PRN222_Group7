using FinalProject_PRN222_Group7.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace FinalProject_PRN222_Group7.Api;

[ApiController]
[Route("api/benchmarks")]
[Authorize]
public class BenchmarksApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<BenchmarksApiController> _logger;

    public BenchmarksApiController(AppDbContext context, ILogger<BenchmarksApiController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public record BenchmarkRunDto(
        int Id,
        string Name,
        string ChunkingStrategy,
        int ChunkSize,
        int ChunkOverlap,
        string EmbeddingModel,
        int TotalQuestions,
        double Faithfulness,
        double AnswerRelevancy,
        double ContextPrecision,
        double ContextRecall,
        double OverallAccuracy,
        string? ResultsJson,
        DateTime RunAt,
        string? RunByEmail,
        string? RunByFullName
    );

    public record BenchmarkComparisonDto(string Name, string ChunkingStrategy, int ChunkSize, int ChunkOverlap, double OverallAccuracy, DateTime RunAt);

    public record BenchmarkStatsDto(
        int TotalRuns,
        int FixedRuns,
        int RecursiveRuns,
        double AverageOverallAccuracy,
        double BestOverallAccuracy,
        double AverageFaithfulness,
        double AverageContextPrecision
    );

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var runs = await _context.BenchmarkRuns.OrderByDescending(b => b.RunAt).ToListAsync();
        return Ok(ToDtos(runs));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var run = await _context.BenchmarkRuns.FirstOrDefaultAsync(b => b.Id == id);
        if (run == null) return NotFound(new { message = "Benchmark run not found" });

        return Ok(ToDtos(new[] { run }).First());
    }

    [HttpGet("strategy/{strategy}")]
    public async Task<IActionResult> GetByStrategy(string strategy)
    {
        var normalizedStrategy = strategy.ToLower();
        if (normalizedStrategy != "fixed" && normalizedStrategy != "recursive")
            return BadRequest(new { message = "Invalid strategy. Use 'fixed' or 'recursive'." });

        var runs = await _context.BenchmarkRuns
            .Where(b => b.ChunkingStrategy.ToLower() == normalizedStrategy)
            .OrderByDescending(b => b.RunAt)
            .ToListAsync();

        return Ok(ToDtos(runs));
    }

    [HttpGet("compare")]
    public async Task<IActionResult> CompareLatestRuns()
    {
        var latestFixed = await _context.BenchmarkRuns
            .Where(b => b.ChunkingStrategy.ToLower() == "fixed")
            .OrderByDescending(b => b.RunAt)
            .FirstOrDefaultAsync();

        var latestRecursive = await _context.BenchmarkRuns
            .Where(b => b.ChunkingStrategy.ToLower() == "recursive")
            .OrderByDescending(b => b.RunAt)
            .FirstOrDefaultAsync();

        var result = new List<BenchmarkComparisonDto>();

        if (latestFixed != null)
        {
            result.Add(new BenchmarkComparisonDto(latestFixed.Name, latestFixed.ChunkingStrategy, latestFixed.ChunkSize, latestFixed.ChunkOverlap, latestFixed.OverallAccuracy, latestFixed.RunAt));
        }

        if (latestRecursive != null)
        {
            result.Add(new BenchmarkComparisonDto(latestRecursive.Name, latestRecursive.ChunkingStrategy, latestRecursive.ChunkSize, latestRecursive.ChunkOverlap, latestRecursive.OverallAccuracy, latestRecursive.RunAt));
        }

        return Ok(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStatistics()
    {
        var totalRuns = await _context.BenchmarkRuns.CountAsync();
        var fixedRuns = await _context.BenchmarkRuns.CountAsync(b => b.ChunkingStrategy.ToLower() == "fixed");
        var recursiveRuns = await _context.BenchmarkRuns.CountAsync(b => b.ChunkingStrategy.ToLower() == "recursive");

        var runs = await _context.BenchmarkRuns.ToListAsync();

        var avgOverall = runs.Any() ? runs.Average(b => b.OverallAccuracy) : 0;
        var bestOverall = runs.Any() ? runs.Max(b => b.OverallAccuracy) : 0;
        var avgFaithfulness = runs.Any() ? runs.Average(b => b.Faithfulness) : 0;
        var avgContextPrecision = runs.Any() ? runs.Average(b => b.ContextPrecision) : 0;

        var stats = new BenchmarkStatsDto(totalRuns, fixedRuns, recursiveRuns, avgOverall, bestOverall, avgFaithfulness, avgContextPrecision);
        return Ok(stats);
    }

    [HttpGet("trend")]
    public async Task<IActionResult> GetTrend([FromQuery] int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var runs = await _context.BenchmarkRuns
            .Where(b => b.RunAt >= from)
            .OrderBy(b => b.RunAt)
            .ToListAsync();

        var result = runs.Select(b => new
        {
            b.Id,
            b.Name,
            b.ChunkingStrategy,
            b.OverallAccuracy,
            b.Faithfulness,
            b.ContextPrecision,
            b.ContextRecall,
            b.RunAt
        });
        return Ok(result);
    }

    private IEnumerable<BenchmarkRunDto> ToDtos(IEnumerable<BenchmarkRun> runs)
    {
        return runs.Select(b => new BenchmarkRunDto(
            b.Id,
            b.Name,
            b.ChunkingStrategy,
            b.ChunkSize,
            b.ChunkOverlap,
            b.EmbeddingModel,
            b.TotalQuestions,
            b.Faithfulness,
            b.AnswerRelevancy,
            b.ContextPrecision,
            b.ContextRecall,
            b.OverallAccuracy,
            b.ResultsJson,
            b.RunAt,
            b.RunById,
            null
        ));
    }
}
