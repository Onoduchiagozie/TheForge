using Microsoft.EntityFrameworkCore;
using TheForge.Data;
using TheForge.Middleware;
using TheForge.Services;
using TheForge.Servcies;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.SnakeCaseLower);

// ── SQLite via EF Core ────────────────────────────────────────────────────────
builder.Services.AddDbContext<ForgeDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("ForgeDb")));

// ── FastAPI typed HttpClient ──────────────────────────────────────────────────
// AddHttpClient<FastApiClient> registers BOTH the HttpClient AND FastApiClient
// in DI. Do NOT add a separate AddScoped<IFastApiClient, FastApiClient> —
// that creates a second instance without the configured HttpClient (null BaseAddress).
builder.Services.AddHttpClient<IFastApiClient, FastApiClient>(client =>
{
    var baseUrl = builder.Configuration["FastApi:BaseUrl"] ?? "http://localhost:8000/";
    // Ensure trailing slash — required for relative URL resolution in HttpClient
    if (!baseUrl.EndsWith("/")) baseUrl += "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(600);
});

// ── Domain services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<UserSettingsService>();
builder.Services.AddScoped<UsageService>();
builder.Services.AddScoped<QueryCacheService>();
builder.Services.AddSingleton<SessionService>();

// ── Memory cache ──────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache(opts => opts.SizeLimit = 500);

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "TheForge",
        Description = "OmnissiahCore gateway — Warhammer 40K lore RAG chatbot API.",
        Version = "v1"
    });
    c.AddSecurityDefinition("ApiKey", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Api-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            []
        }
    });
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Auto-migrate SQLite on startup ────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ForgeDbContext>();
    db.Database.Migrate();
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
// Program.cs — find where you register ForgeAuthMiddleware and wrap it:

if (!app.Environment.IsDevelopment())
{
    app.UseMiddleware<ForgeAuthMiddleware>();
}
else
{
    // In Development, inject a fake archmagos user so all controllers work
    // without needing a real DB key. Remove this block before deploying.
    app.Use(async (context, next) =>
    {
        // Fake the ForgeUser that controllers read from HttpContext.Items
        context.Items["ForgeUser"] = new TheForge.Data.Entities.User
        {
            Id           = 1,
            Email        = "dev@theforge.dev",
            Tier         = "archmagos",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow,
            Settings     = new TheForge.Data.Entities.UserSettings
            {
                PreferredMode = "remembrancer",
                TopK          = 10,
                UpdatedAt     = DateTime.UtcNow,
            }
        };
        await next();
    });
}
app.UseAuthorization();
app.MapControllers();

app.Run();
// using Microsoft.EntityFrameworkCore;
// using TheForge.Data;
// using TheForge.Middleware;
// using TheForge.Services;
// using TheForge.Servcies;
//
// var builder = WebApplication.CreateBuilder(args);
//
// // ── Controllers ───────────────────────────────────────────────────────────────
// builder.Services.AddControllers()
//     .AddJsonOptions(opts =>
//         opts.JsonSerializerOptions.PropertyNamingPolicy =
//             System.Text.Json.JsonNamingPolicy.SnakeCaseLower);
//
// // ── SQLite via EF Core ────────────────────────────────────────────────────────
// builder.Services.AddDbContext<ForgeDbContext>(opts =>
//     opts.UseSqlite(builder.Configuration.GetConnectionString("ForgeDb")));
//
// // ── FastAPI typed HttpClient ──────────────────────────────────────────────────
// // AddHttpClient<FastApiClient> registers BOTH the HttpClient AND FastApiClient
// // in DI. Do NOT add a separate AddScoped<IFastApiClient, FastApiClient> —
// // that creates a second instance without the configured HttpClient (null BaseAddress).
// builder.Services.AddHttpClient<IFastApiClient, FastApiClient>(client =>
// {
//     var baseUrl = builder.Configuration["FastApi:BaseUrl"] ?? "http://localhost:8000/";
//     // Ensure trailing slash — required for relative URL resolution in HttpClient
//     if (!baseUrl.EndsWith("/")) baseUrl += "/";
//     client.BaseAddress = new Uri(baseUrl);
//     client.Timeout = TimeSpan.FromSeconds(600);
// });
//
// // ── Domain services ───────────────────────────────────────────────────────────
// builder.Services.AddScoped<UserSettingsService>();
// builder.Services.AddScoped<UsageService>();
// builder.Services.AddScoped<QueryCacheService>();
// builder.Services.AddSingleton<SessionService>();
//
// // ── Memory cache ──────────────────────────────────────────────────────────────
// builder.Services.AddMemoryCache(opts => opts.SizeLimit = 500);
//
// // ── Swagger ───────────────────────────────────────────────────────────────────
// builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("v1", new()
//     {
//         Title = "TheForge",
//         Description = "OmnissiahCore gateway — Warhammer 40K lore RAG chatbot API.",
//         Version = "v1"
//     });
//     c.AddSecurityDefinition("ApiKey", new()
//     {
//         In = Microsoft.OpenApi.Models.ParameterLocation.Header,
//         Name = "X-Api-Key",
//         Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
//     });
//     c.AddSecurityRequirement(new()
//     {
//         {
//             new()
//             {
//                 Reference = new()
//                 {
//                     Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
//                     Id = "ApiKey"
//                 }
//             },
//             []
//         }
//     });
// });
//
// // ── CORS ──────────────────────────────────────────────────────────────────────
// builder.Services.AddCors(opts =>
//     opts.AddDefaultPolicy(p =>
//         p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
//
// var app = builder.Build();
//
// // ── Auto-migrate SQLite on startup ────────────────────────────────────────────
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<ForgeDbContext>();
//     db.Database.Migrate();
// }
//
// // ── Pipeline ──────────────────────────────────────────────────────────────────
//  
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI();
// }
//
// app.UseCors();
//
// app.UseRouting(); // ✅ REQUIRED before middleware
//
// app.UseMiddleware<ForgeAuthMiddleware>(); // ✅ correct middleware
//
// app.UseAuthorization(); // optional (you’re not using [Authorize])
//
// app.MapControllers();
//
// app.Run();