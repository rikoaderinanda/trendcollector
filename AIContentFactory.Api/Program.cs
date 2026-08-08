using System.Reflection;
using Swashbuckle.AspNetCore.Filters;
using AIContentFactory.Api.AI;
using AIContentFactory.Api.Configuration;
using AIContentFactory.Api.Data;
using AIContentFactory.Api.Repositories;
using AIContentFactory.Api.Services;
using AIContentFactory.Api.Transcript;
using AIContentFactory.Api.Workers;

var builder = WebApplication.CreateBuilder(args);

// Local (non-committed) settings for secrets: appsettings.Local.json
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Options pattern
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<TrendDiscoveryOptions>(
    builder.Configuration.GetSection(TrendDiscoveryOptions.SectionName));

builder.Services.Configure<TrendCollectorOptions>(
    builder.Configuration.GetSection(TrendCollectorOptions.SectionName));

builder.Services.Configure<YouTubeOptions>(
    builder.Configuration.GetSection(YouTubeOptions.SectionName));

builder.Services.Configure<KnowledgeExtractionOptions>(
    builder.Configuration.GetSection(KnowledgeExtractionOptions.SectionName));

builder.Services.Configure<TrackingModeOptions>(
    builder.Configuration.GetSection(TrackingModeOptions.SectionName));

// Data access
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<DbInitializer>();

// ---- Agent 0: Trend Discovery repositories ----
builder.Services.AddScoped<ITrendKeywordRepository, TrendKeywordRepository>();
builder.Services.AddScoped<ITrendDiscoveryJobRepository, TrendDiscoveryJobRepository>();
builder.Services.AddScoped<ITrendDiscoveryPromptHistoryRepository, TrendDiscoveryPromptHistoryRepository>();

// ---- Agent 1: Trend Collector repositories ----
builder.Services.AddScoped<IPlatformRepository, PlatformRepository>();
builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IQuotaRepository, QuotaRepository>();

// ---- Agent 2: Knowledge Extraction repositories ----
builder.Services.AddScoped<IKnowledgeExtractionQueueRepository, KnowledgeExtractionQueueRepository>();
builder.Services.AddScoped<IVideoTranscriptRepository, VideoTranscriptRepository>();
builder.Services.AddScoped<IVideoKnowledgeRepository, VideoKnowledgeRepository>();
builder.Services.AddScoped<IVideoKnowledgeRawRepository, VideoKnowledgeRawRepository>();
builder.Services.AddScoped<IVideoMetadataRepository, VideoMetadataRepository>();

// ---- External HTTP clients ----
builder.Services.AddHttpClient<IYouTubeApiService, YouTubeApiService>();
builder.Services.AddHttpClient<ITranscriptProvider, YouTubeTranscriptProvider>();

// ---- Trend Discovery AI provider selection ----
var discoveryAiProvider = builder.Configuration.GetValue<string>($"{TrendDiscoveryOptions.SectionName}:AIProvider") ?? "OpenAICompatible";

switch (discoveryAiProvider)
{
    case "OpenAICompatible":
        builder.Services.AddHttpClient<ITrendDiscoveryAIProvider, DiscoveryOpenAICompatibleProvider>();
        break;
    case "NoOp":
    default:
        builder.Services.AddSingleton<ITrendDiscoveryAIProvider, DiscoveryNoOpAIProvider>();
        break;
}

// ---- Knowledge Extraction AI provider selection ----
var extractionAiProvider = builder.Configuration.GetValue<string>($"{KnowledgeExtractionOptions.SectionName}:AIProvider") ?? "OpenAICompatible";

switch (extractionAiProvider)
{
    case "OpenAICompatible":
        builder.Services.AddHttpClient<IKnowledgeExtractionProvider, ExtractionOpenAICompatibleProvider>();
        break;
    case "NoOp":
    default:
        builder.Services.AddSingleton<IKnowledgeExtractionProvider, ExtractionNoOpAIProvider>();
        break;
}

// ---- Core services (all three agents run in-process) ----
builder.Services.AddSingleton<CollectionCoordinator>();
builder.Services.AddScoped<TrendDiscoveryService>();
builder.Services.AddScoped<StatisticsCalculator>();
builder.Services.AddScoped<IQuotaTracker, QuotaTracker>();
builder.Services.AddScoped<TrendCollectorService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IKnowledgeExtractionService, KnowledgeExtractionService>();

// ---- Background services ----
builder.Services.AddHostedService<TrendCollectionBackgroundService>();
builder.Services.AddHostedService<TrendTrackingBackgroundService>();
builder.Services.AddHostedService<KnowledgeExtractionBackgroundService>();
builder.Services.AddHostedService<DataRetentionBackgroundService>();

// CORS policy for the React dashboard (development)
const string dashboardCorsPolicy = "DashboardFrontendCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(dashboardCorsPolicy, policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Swagger examples
builder.Services.AddSwaggerExamplesFromAssemblies(Assembly.GetExecutingAssembly());

// Controllers
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
    options.ExampleFilters();
});

var app = builder.Build();

// Enable Swagger UI in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply database schema on startup (idempotent SQL)
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.UseHttpsRedirection();

app.UseCors(dashboardCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();