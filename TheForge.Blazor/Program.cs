using TheForge.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ForgeApiService — typed HttpClient pointing at TheForge .NET API
builder.Services.AddHttpClient<ForgeApiService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ForgeApi:BaseUrl"] ?? "http://localhost:5157");
    client.Timeout = TimeSpan.FromSeconds(600);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<TheForge.Blazor.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
