using AIPRReviewer.Models;

namespace AIPRReviewer.Services;

public interface IGeminiService
{
    Task<(bool Success, string Summary, List<ReviewFinding> Findings, string Error)> ReviewCodeAsync(List<FileChange> files);
}