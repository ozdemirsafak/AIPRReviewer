using System.Text.Json;
using System.Text.RegularExpressions;
using AIPRReviewer.Models;

namespace AIPRReviewer.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;

    public GitHubService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // GitHub API, User-Agent header'ı olmadan isteği reddediyor
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "AIPRReviewer");
        }
    }

    public async Task<(bool Success, string PrTitle, List<FileChange> Files, string Error)> GetPullRequestChangesAsync(string prUrl)
    {
        // Örnek URL: https://github.com/owner/repo/pull/12
        var match = Regex.Match(prUrl.Trim(), @"github\.com/([^/]+)/([^/]+)/pull/(\d+)");

        if (!match.Success)
        {
            return (false, string.Empty, new List<FileChange>(),
                "Geçersiz PR linki. Beklenen format: https://github.com/owner/repo/pull/123");
        }

        var owner = match.Groups[1].Value;
        var repo = match.Groups[2].Value;
        var prNumber = match.Groups[3].Value;

        try
        {
            // 1. PR detaylarını çek (başlık için)
            var prResponse = await _httpClient.GetAsync(
                $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}");

            if (!prResponse.IsSuccessStatusCode)
            {
                if (prResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (false, string.Empty, new List<FileChange>(), "PR bulunamadı. Repo public mi ve PR numarası doğru mu kontrol edin.");

                if (prResponse.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return (false, string.Empty, new List<FileChange>(), "GitHub API rate limit aşıldı. Birkaç dakika sonra tekrar deneyin.");

                return (false, string.Empty, new List<FileChange>(), $"GitHub API hatası: {prResponse.StatusCode}");
            }

            var prJson = await prResponse.Content.ReadAsStringAsync();
            using var prDoc = JsonDocument.Parse(prJson);
            var prTitle = prDoc.RootElement.GetProperty("title").GetString() ?? "Başlıksız PR";

            // 2. Değişen dosyaları çek
            var filesResponse = await _httpClient.GetAsync(
                $"https://api.github.com/repos/{owner}/{repo}/pulls/{prNumber}/files");

            if (!filesResponse.IsSuccessStatusCode)
            {
                return (false, string.Empty, new List<FileChange>(), "PR dosyaları alınamadı.");
            }

            var filesJson = await filesResponse.Content.ReadAsStringAsync();
            using var filesDoc = JsonDocument.Parse(filesJson);

            var changes = new List<FileChange>();

            foreach (var file in filesDoc.RootElement.EnumerateArray())
            {
                changes.Add(new FileChange
                {
                    FileName = file.GetProperty("filename").GetString() ?? "",
                    Patch = file.TryGetProperty("patch", out var patchProp) ? patchProp.GetString() ?? "" : "(binary dosya, diff gösterilemiyor)",
                    Additions = file.GetProperty("additions").GetInt32(),
                    Deletions = file.GetProperty("deletions").GetInt32()
                });
            }

            if (changes.Count == 0)
            {
                return (false, string.Empty, new List<FileChange>(), "Bu PR'da incelenebilecek dosya değişikliği bulunamadı.");
            }

            return (true, prTitle, changes, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, new List<FileChange>(), $"Beklenmeyen hata: {ex.Message}");
        }
    }
}