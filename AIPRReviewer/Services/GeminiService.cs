using System.Text;
using System.Text.Json;
using AIPRReviewer.Models;
using Microsoft.Extensions.Configuration;

namespace AIPRReviewer.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GeminiService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key bulunamadı. User Secrets kontrol edin.");
    }

    public async Task<(bool Success, string Summary, List<ReviewFinding> Findings, string Error)> ReviewCodeAsync(List<FileChange> files)
    {
        // Diff'leri tek bir metin haline getir (çok büyükse kısalt)
        var diffText = new StringBuilder();
        foreach (var file in files.Take(15)) // çok fazla dosya varsa ilk 15'i al
        {
            diffText.AppendLine($"=== Dosya: {file.FileName} (+{file.Additions} -{file.Deletions}) ===");
            diffText.AppendLine(file.Patch.Length > 3000 ? file.Patch[..3000] + "\n...(kısaltıldı)" : file.Patch);
            diffText.AppendLine();
        }

        var prompt = $@"
Sen deneyimli bir kod inceleme uzmanısın. Aşağıdaki GitHub Pull Request diff'ini incele.

Şu kriterlere göre değerlendir:
- Okunabilirlik ve isimlendirme kalitesi
- Best practice uyumu (dile özgü doğru kullanım)
- Potansiyel bug veya mantık hataları
- Kod tekrarı veya basitleştirilebilecek kısımlar

SADECE aşağıdaki JSON formatında yanıt ver, başka hiçbir açıklama veya markdown ekleme:

{{
  ""summary"": ""PR'ın genel değerlendirmesi, 2-3 cümle, Türkçe"",
  ""findings"": [
    {{ ""fileName"": ""dosya adı"", ""severity"": ""Bilgi|Öneri|Uyarı"", ""comment"": ""bulgu açıklaması, Türkçe"" }}
  ]
}}

İncelenecek diff:
{diffText}
";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            }
        };

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var response = await _httpClient.PostAsJsonAsync(url, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return (false, string.Empty, new List<ReviewFinding>(), $"Gemini API hatası ({response.StatusCode}): {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            var rawText = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            // Gemini bazen JSON'ı ```json ... ``` bloğu içinde döndürür, temizleyelim
            var cleanJson = rawText.Trim();
            if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Replace("```json", "").Replace("```", "").Trim();
            }

            using var resultDoc = JsonDocument.Parse(cleanJson);
            var summary = resultDoc.RootElement.GetProperty("summary").GetString() ?? "";

            var findings = new List<ReviewFinding>();
            if (resultDoc.RootElement.TryGetProperty("findings", out var findingsArray))
            {
                foreach (var item in findingsArray.EnumerateArray())
                {
                    findings.Add(new ReviewFinding
                    {
                        FileName = item.GetProperty("fileName").GetString() ?? "",
                        Severity = item.GetProperty("severity").GetString() ?? "Bilgi",
                        Comment = item.GetProperty("comment").GetString() ?? ""
                    });
                }
            }

            return (true, summary, findings, string.Empty);
        }
        catch (JsonException)
        {
            return (false, string.Empty, new List<ReviewFinding>(), "AI yanıtı işlenemedi, lütfen tekrar deneyin.");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, new List<ReviewFinding>(), $"Beklenmeyen hata: {ex.Message}");
        }
    }
}