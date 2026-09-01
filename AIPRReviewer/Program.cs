using AIPRReviewer.Services;

var builder = WebApplication.CreateBuilder(args);

// HttpClient'lar? DI'a kaydet
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();