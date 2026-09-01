namespace AIPRReviewer.Models;

// Kullanıcının arayüzden gönderdiği istek
public class ReviewRequest
{
    public string PullRequestUrl { get; set; } = string.Empty;
}

// GitHub'dan çektiğimiz tek bir dosya değişikliği
public class FileChange
{
    public string FileName { get; set; } = string.Empty;
    public string Patch { get; set; } = string.Empty; // diff içeriği
    public int Additions { get; set; }
    public int Deletions { get; set; }
}

// Gemini'nin döndürdüğü tek bir bulgu/yorum
public class ReviewFinding
{
    public string FileName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Bilgi", "Öneri", "Uyarı"
    public string Comment { get; set; } = string.Empty;
}

// Kullanıcıya dönecek son rapor
public class ReviewResult
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string PullRequestTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty; // genel özet
    public List<ReviewFinding> Findings { get; set; } = new();
}