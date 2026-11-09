using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using TheForge.Components;
using TheForge.Data;
using TheForge.Features.SceneArchive.Data;
using TheForge.Services;
using TheForge.Services.Interfaces;
using TheForge.Features.Workspace.Services;
using TheForge.Features.SceneArchive.Services;

var builder = WebApplication.CreateBuilder(args);

// Browser tests use isolated in-memory keys, without touching the developer's key ring.
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.WebHost.UseStaticWebAssets();
}

// The deployment must explicitly configure the bind address and host policy.
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5157");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Scene Archive: separate, read-only DbContext against battlescenes.db (an independent
// SQLite file populated externally by the OmnissiahCore scene-weaving pipeline — this app
// never migrates or writes to it, so it's deliberately not on ForgeDbContext).
builder.Services.AddDbContext<SceneArchiveDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SceneArchiveDb")));
builder.Services.AddScoped<ISceneArchiveRepository, SceneArchiveRepository>();
// Ranking uses the shared IMemoryCache; keep its repository/DbContext dependency scoped.
builder.Services.AddScoped<ISceneRankingService, SceneRankingService>();
builder.Services.AddScoped<ISceneArchiveService, SceneArchiveService>();
builder.Services.AddScoped<IArchiveAccessRequestService, ArchiveAccessRequestService>();

builder.Services.AddHttpClient<IFastApiClient, FastApiClient>(client =>
{
    var baseUrl = builder.Configuration["FastApi:BaseUrl"] ?? "http://localhost:8000/";
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    client.BaseAddress = new Uri(baseUrl);
    // API requests must reach Oracle rather than ngrok's browser interstitial.
    client.DefaultRequestHeaders.TryAddWithoutValidation("ngrok-skip-browser-warning", "1");
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddMemoryCache(options => options.SizeLimit = 500);
builder.Services.AddHttpClient("ForgeApi", client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<IForgeApiService, ForgeApiService>();
builder.Services.AddScoped<QueryStreamParser>();
builder.Services.AddScoped<WorkspaceQueryService>();
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddSingleton<StaticAssetUrls>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "TheForge",
        Description = "OmnissiahCore gateway API for the FastAPI RAG backend.",
        Version = "v1"
    });

});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseCors();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
