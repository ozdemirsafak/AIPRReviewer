using AIPRReviewer.Models;
using AIPRReviewer.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIPRReviewer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IGitHubService _gitHubService;
    private readonly IGeminiService _geminiService;

    public ReviewController(IGitHubService gitHubService, IGeminiService geminiService)
    {
        _gitHubService = gitHubService;
        _geminiService = geminiService;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<ReviewResult>> Analyze([FromBody] ReviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PullRequestUrl))
        {
            return BadRequest(new ReviewResult { Success = false, Error = "PR linki boş olamaz." });
        }

        // 1. GitHub'dan PR değişikliklerini çek
        var (githubSuccess, prTitle, files, githubError) = await _gitHubService.GetPullRequestChangesAsync(request.PullRequestUrl);

        if (!githubSuccess)
        {
            return BadRequest(new ReviewResult { Success = false, Error = githubError });
        }

        // 2. Gemini ile incele
        var (geminiSuccess, summary, findings, geminiError) = await _geminiService.ReviewCodeAsync(files);

        if (!geminiSuccess)
        {
            return BadRequest(new ReviewResult { Success = false, Error = geminiError });
        }

        // 3. Sonucu döndür
        return Ok(new ReviewResult
        {
            Success = true,
            PullRequestTitle = prTitle,
            Summary = summary,
            Findings = findings
        });
    }
}