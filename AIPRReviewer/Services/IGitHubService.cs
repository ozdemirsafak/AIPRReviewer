using AIPRReviewer.Models;

namespace AIPRReviewer.Services;

public interface IGitHubService
{
    Task<(bool Success, string PrTitle, List<FileChange> Files, string Error)> GetPullRequestChangesAsync(string prUrl);
}