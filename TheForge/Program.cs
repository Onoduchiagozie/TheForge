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

// Testing-only: bind to all network interfaces (not just localhost) so the
// ngrok tunnel can reach Kestrel. AllowedHosts "*" is set in appsettings.json.
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5157");


builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// builder.Services.AddDbContext<ForgeDbContext>(options =>
//     options.UseSqlite(builder.Configuration.GetConnectionString("ForgeDb")));

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

// GOLD: User settings/tier caps are intentionally not registered in the pre-Gold build.
// builder.Services.AddScoped<UserSettingsService>();
// GOLD: Usage throttling is intentionally not registered in the pre-Gold build.
// builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<QueryCacheService>();
// GOLD: Context/session memory is intentionally not registered in the pre-Gold build.
// builder.Services.AddSingleton<SessionService>();
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

    // GOLD: API-key auth is intentionally not advertised in the pre-Gold build.
    // options.AddSecurityDefinition("ApiKey", new()
    // {
    //     In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    //     Name = "X-Api-Key",
    //     Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    // });
    //
    // options.AddSecurityRequirement(new()
    // {
    //     {
    //         new()
    //         {
    //             Reference = new()
    //             {
    //                 Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
    //                 Id = "ApiKey"
    //             }
    //         },
    //         []
    //     }
    // });
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<ForgeDbContext>();
//     db.Database.Migrate();
// }

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseCors();

// GOLD: Authentication is intentionally disabled in the pre-Gold build.
// if (app.Environment.IsDevelopment())
// {
//     app.Use(async (context, next) =>
//     {
//         if (context.Request.Path.StartsWithSegments("/api"))
//         {
//             context.Items["ForgeUser"] = new User
//             {
//                 Id = 1,
//                 Email = "dev@theforge.dev",
//                 Tier = "archmagos",
//                 IsActive = true,
//                 CreatedAt = DateTime.UtcNow,
//                 Settings = new UserSettings
//                 {
//                     PreferredMode = "remembrancer",
//                     TopK = 10,
//                     CandidatePool = 20,
//                     StitchingWindow = 5,
//                     UpdatedAt = DateTime.UtcNow
//                 }
//             };
//         }
//
//         await next();
//     });
// }
// else
// {
//     app.UseMiddleware<ForgeAuthMiddleware>();
// }

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
































