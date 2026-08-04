using TrendCollector.Api.Configuration;
using TrendCollector.Api.Data;
using TrendCollector.Api.Repositories;
using TrendCollector.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Local (non-committed) settings for secrets: appsettings.Local.json
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Options pattern
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.SectionName));

builder.Services.Configure<YouTubeOptions>(
    builder.Configuration.GetSection(YouTubeOptions.SectionName));

// Data access
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<DbInitializer>();

// Repositories
builder.Services.AddScoped<IPlatformRepository, PlatformRepository>();
builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<IJobRepository, JobRepository>();

// Services
builder.Services.AddHttpClient<IYouTubeApiService, YouTubeApiService>();
builder.Services.AddScoped<StatisticsCalculator>();
builder.Services.AddScoped<TrendCollectorService>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Apply database schema on startup (idempotent SQL)
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();