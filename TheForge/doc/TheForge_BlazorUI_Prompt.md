# THE FORGE — BLACK OCULUS
## Exhaustive Blazor Server UI Implementation Prompt
### For Use With a Coding Assistant That Has Full Codebase Access
---

> **HOW TO USE THIS DOCUMENT**
> Feed this entire document to your coding assistant along with codebase access.
> Work through it **section by section**, **task by task**.
> Each task is atomic — one clear action, one clear output.
> At the end of each section, the assistant must drop a **CHECKPOINT** comment
> in the code marking what was completed and what comes next.
> Do not skip tasks. Do not combine tasks that are listed separately.
> If a task depends on another section, it will say so explicitly.

---

## SECTION 0 — CONTEXT AND CONSTRAINTS

### 0.1 — What This Project Is

The project is called **TheForge**, with the UI sub-layer called **Black Oculus**.

TheForge is NOT the AI. It is the middleware, API gateway, and frontend orchestration layer.

The stack has three layers:
1. **OmnissiahCore** — Python FastAPI microservice. Handles RAG retrieval, FAISS vector search, BM25 keyword search, CrossEncoder reranking, and Ollama LLM generation. Already exists. Do not modify it.
2. **TheForge API** — ASP.NET Core 8 Web API. Already exists. Acts as the BFF (Backend For Frontend). Handles auth, user tiers, session management, quota tracking, caching, and proxies requests to OmnissiahCore.
3. **Black Oculus** — Blazor Server frontend. **This is what you are building.** It lives inside the TheForge project. It talks only to TheForge API endpoints under `/api/`. It never calls OmnissiahCore directly.

### 0.2 — Thematic Identity

The UI is a **Warhammer 40K lore research terminal**. Every visual decision should reinforce:
- You are interfacing with an ancient, powerful archive
- Information is sacred, retrieved from physical books, not guessed
- The aesthetic is dark, precise, imperial, mechanical, metalic , even with little design of rust

This is also a **portfolio project**. It must be professional enough to show recruiters. The immersive theme is intentional, not accidental.

 
```
// [DOMAIN] — Replace this string/label if switching to a different lore domain
```

This includes: placeholder text, mode labels, faction names, theme names, and any hardcoded lore references in UI copy.

### 0.4 — Hard Constraints

- **No JavaScript interop** unless there is absolutely no Blazor-native alternative. If JS interop is used, it must be isolated in a single `Interop/` folder and clearly documented.
- **No third-party component libraries** (no MudBlazor, no Radzen, no Telerik). Vanilla CSS and Blazor built-in components only.
- **All CSS in scoped `.razor.css` files** alongside their component. No inline `style=` attributes in Razor markup.
- **No direct calls from Blazor to OmnissiahCore**. All calls go through TheForge `/api/` endpoints.
- **Blazor Server Interactive rendering mode** throughout. No WebAssembly. No static SSR for interactive pages.
- All `.razor.css` files must use **CSS custom properties (variables)** for all colors, fonts, and spacing so the theme system can override them at runtime.

### 0.5 — Existing TheForge API Endpoints (Do Not Change These)

```
AUTH
  POST /api/auth/register     { email, username, password }
  POST /api/auth/login        { email, password }
  POST /api/auth/new-key

QUERY
   POST /api/query/narrate/stream  SSE streaming narrator ← PRIMARY
  POST /api/query/explore     Sync explorer
  POST /api/query/inspect     Sync diagnostics, no LLM
 
SYSTEM
  GET  /api/health
  GET  /api/info
  GET  /api/sources
  GET  /api/sources/{name}
  GET  /api/memory?session_id=
  DELETE /api/memory?session_id=
  GET  /api/settings
  GET  /api/settings/tier
```

### 0.6 — Request Contract (All Query Endpoints)

```json
{
  "query": "string (required)",
  "session_id": "string (optional, default: 'default')",
  "top_k": "integer 1–20 (optional)",
  "candidate_pool": "integer 1–100 (optional)",
  "stitching_window": "integer 0–6 (optional)",
  "book_filter": "string or null (optional)",
  "source_filter": ["string"] or null
}
```

**Important:** There is NO `mode` field in the request body. Mode is determined by which endpoint you call. Do not add a `mode` field to request payloads.

### 0.7 — SSE Stream Format

```
data: token text here       ← individual token, append to response
data: __SOURCES__:[{...}]   ← JSON array of sources, parse and display
data: [DONE]                ← stream complete, stop loop
data: [ERROR] message here  ← error, display in red, stop loop
```

Sources JSON shape:
```json
[
  {
    "source": "filename.pdf",
    "chapter": "Chapter 12 or 'unknown'",
    "stitch_range": "10-14",
    "score": 0.87
  }
]
```

[//]: # ()
[//]: # (### 0.8 — User Tier Capabilities)

[//]: # ()
[//]: # (The API enforces these. The UI should reflect them:)

[//]: # ()
[//]: # (| Tier       | Modes Available                          | Daily Limit | top_k max | pool max |)

[//]: # (|------------|------------------------------------------|-------------|-----------|----------|)

[//]: # (| free       | remembrancer only                        | 10/day      | 4         | 10       |)

[//]: # (| adept      | remembrancer, narrator, explorer         | 100/day     | 8         | 30       |)

[//]: # (| archmagos  | all modes + inspect                      | unlimited   | 10        | 50       |)

[//]: # ()
[//]: # (---)

## SECTION 1 — PROJECT STRUCTURE

### 1.1 — Verify Existing Project Structure

**Task 1.1.1:** Open `TheForge.csproj` and confirm the project targets `net8.0`.

**Task 1.1.2:** Open `Program.cs` and identify:
- Where services are registered
- Where middleware is configured
- Whether Blazor Server is already configured (`AddRazorComponents`, `MapRazorComponents`)
- Whether `ForgeAuthMiddleware` is registered and where in the pipeline

**Task 1.1.3:** Open `Components/` folder and note what already exists (likely `App.razor`, `Routes.razor`, `_Imports.razor`, `Layout/`).

**Task 1.1.4:** Confirm `wwwroot/` exists and note what CSS or JS files are already there.

### 1.2 — Create Folder Structure

**Task 1.2.1:** Create the following folders inside `Components/` if they do not exist:
```
Components/
  Pages/
    Auth/
    Workspace/
    Admin/
  Layout/
  Shared/
    StatusBar/
    SourcesPanel/
    InspectDrawer/
    AdvancedControls/
    ThemeController/
    SuggestionChips/
    Memory/
```

**Task 1.2.2:** Create the following folders at the project root if they do not exist:
```
Services/
  Interfaces/
Models/
  Api/
  UI/
Themes/
wwwroot/
  css/
  fonts/
```

**Task 1.2.3:** After creating the folder structure, add a file called `_structure.md` in the project root documenting every folder and its purpose. This is for onboarding and token-limit recovery. Update this file at the end of each section.

---

## SECTION 2 — MODELS AND DATA CONTRACTS

> These are C# classes that mirror the API contracts. Create all in `Models/Api/`.

### 2.1 — Query Models

**Task 2.1.1:** Create `Models/Api/QueryRequest.cs`:
```csharp
public class QueryRequest
{
    public string Query { get; set; } = "";
    public string SessionId { get; set; } = "default";
    
    public string? BookFilter { get; set; }
    public List<string>? SourceFilter { get; set; }
    // NOTE: No Mode field. Mode is determined by endpoint route.
}
```

**Task 2.1.2:** Create `Models/Api/QueryResponse.cs`:
```csharp
public class QueryResponse
{
    public string Query { get; set; } = "";
    public string Response { get; set; } = "";
    public List<SourceResult> Sources { get; set; } = new();
    public int ChunksUsed { get; set; }
}
```

**Task 2.1.3:** Create `Models/Api/SourceResult.cs`:
```csharp
public class SourceResult
{
    public string Source { get; set; } = "";
    public string Chapter { get; set; } = "unknown";
    public string? StitchRange { get; set; }
    public double Score { get; set; }
}
```

**Task 2.1.4:** Create `Models/Api/InspectResponse.cs` to hold diagnostic data:
```csharp
public class InspectResponse
{
    public List<HitResult> FaissHits { get; set; } = new();
    public List<HitResult> Bm25Hits { get; set; } = new();
    public List<HitResult> GroundedHits { get; set; } = new();
    public List<HitResult> StitchedHits { get; set; } = new();
    public List<string> QueryTerms { get; set; } = new();
}

public class HitResult
{
    public string Source { get; set; } = "";
    public string? Chapter { get; set; }
    public string? ChunkRange { get; set; }
    public double Score { get; set; }
    public string? TextPreview { get; set; }
}
```

### 2.2 — Auth Models

**Task 2.2.1:** Create `Models/Api/LoginRequest.cs`:
```csharp
public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}
```

**Task 2.2.2:** Create `Models/Api/RegisterRequest.cs`:
```csharp
public class RegisterRequest
{
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
```

**Task 2.2.3:** Create `Models/Api/AuthResponse.cs`:
```csharp
public class AuthResponse
{
    public string ApiKey { get; set; } = "";
    public string? Token { get; set; }
    public string Username { get; set; } = "";
    public string Tier { get; set; } = "free";
    public string SessionId { get; set; } = "";
}
```

### 2.3 — System Models

**Task 2.3.1:** Create `Models/Api/HealthResponse.cs`:
```csharp
public class HealthResponse
{
    public string Status { get; set; } = "";
    public string? Model { get; set; }
    public string? Profile { get; set; }
    public int? ChunksLoaded { get; set; }
    public bool RetrieverReady { get; set; }
}
```

**Task 2.3.2:** Create `Models/Api/SourceInfo.cs`:
```csharp
public class SourceInfo
{
    public string Name { get; set; } = "";
    public int? ChunkCount { get; set; }
    public string? FileType { get; set; }
}
```

### 2.4 — UI State Models

**Task 2.4.1:** Create `Models/UI/WorkspaceMode.cs`:
```csharp
public enum WorkspaceMode
{
    Narrator,  // [DOMAIN] rename if switching lore domain
    Explorer   // [DOMAIN] rename if switching lore domain
}
```

**Task 2.4.2:** Create `Models/UI/StreamState.cs`:
```csharp
public enum StreamState
{
    Idle,
    Streaming,
    Complete,
    Error,
    Cancelled
}
```

**Task 2.4.3:** Create `Models/UI/ThemePreset.cs`:
```csharp
public enum ThemePreset
{
    // [DOMAIN] All theme names below are Warhammer-specific.
    // Replace with domain-appropriate presets if switching domains.
    MechanicusRed,
    ImperialGold,
    UltramarineBlue,
    ChaosVoid,
    DarkArchive,
    TacticalSlate
}
```

---

## SECTION 3 — SERVICES

### 3.1 — IForgeApiService Interface

**Task 3.1.1:** Create `Services/Interfaces/IForgeApiService.cs`:

Define the following interface methods:
```csharp
public interface IForgeApiService
{
    Task<HealthResponse?> GetHealthAsync();
    Task<List<SourceInfo>> GetSourcesAsync();
    Task<QueryResponse?> QueryExploreAsync(QueryRequest request, string apiKey);
    Task<InspectResponse?> QueryInspectAsync(QueryRequest request, string apiKey);
    Task<HttpResponseMessage> BeginNarrateStreamAsync(QueryRequest request, string apiKey, CancellationToken ct);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task DeleteMemoryAsync(string sessionId, string apiKey);
    Task<List<MemoryTurn>> GetMemoryAsync(string sessionId, string apiKey);
}
```

**Task 3.1.2:** Create `Models/Api/MemoryTurn.cs`:
```csharp
public class MemoryTurn
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}
```

### 3.2 — ForgeApiService Implementation

**Task 3.2.1:** Create `Services/ForgeApiService.cs`. Inject `IHttpClientFactory`. Resolve a named client called `"ForgeApi"`.

**Task 3.2.2:** Implement `GetHealthAsync()`:
- GET `/api/health`
- No auth header required
- Deserialize to `HealthResponse`
- On any exception, return null (never throw to the UI)

**Task 3.2.3:** Implement `GetSourcesAsync()`:
- GET `/api/sources`
- No auth header required
- Deserialize to `List<SourceInfo>`
- On any exception, return empty list

**Task 3.2.4:** Implement `QueryExploreAsync(QueryRequest request, string apiKey)`:
- POST `/api/query/explore`
- Add header: `X-Api-Key: {apiKey}`
- Serialize request body as JSON
- Deserialize response to `QueryResponse`
- On non-2xx response, return null

**Task 3.2.5:** Implement `QueryInspectAsync(QueryRequest request, string apiKey)`:
- POST `/api/query/inspect`
- Add header: `X-Api-Key: {apiKey}`
- Serialize request body as JSON
- Deserialize response to `InspectResponse`
- On non-2xx response, return null

**Task 3.2.6:** Implement `BeginNarrateStreamAsync(QueryRequest request, string apiKey, CancellationToken ct)`:
- Build an `HttpRequestMessage` with method POST and URL `/api/query/narrate/stream`
- Add header: `X-Api-Key: {apiKey}`
- Serialize request body to JSON and set as StringContent with `application/json`
- Call `SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)`
- Return the raw `HttpResponseMessage` — do NOT read the stream here
- The caller (the Blazor component) will read the stream line by line

**Task 3.2.7:** Implement `LoginAsync(LoginRequest request)`:
- POST `/api/auth/login`
- No auth header
- Return `AuthResponse` on success
- Return null on failure

**Task 3.2.8:** Implement `RegisterAsync(RegisterRequest request)`:
- POST `/api/auth/register`
- No auth header
- Return `AuthResponse` on success
- Return null on failure

**Task 3.2.9:** Implement `DeleteMemoryAsync(string sessionId, string apiKey)`:
- DELETE `/api/memory?session_id={sessionId}`
- Add header: `X-Api-Key: {apiKey}`
- Fire and forget, no return value

**Task 3.2.10:** Implement `GetMemoryAsync(string sessionId, string apiKey)`:
- GET `/api/memory?session_id={sessionId}`
- Add header: `X-Api-Key: {apiKey}`
- Return `List<MemoryTurn>`

### 3.3 — IAuthStateService Interface

**Task 3.3.1:** Create `Services/Interfaces/IAuthStateService.cs`:
```csharp
public interface IAuthStateService
{
    bool IsAuthenticated { get; }
    string? ApiKey { get; }
    string? Username { get; }
    string? Tier { get; }
    string SessionId { get; }
    event Action? OnAuthStateChanged;
    void SetAuthState(AuthResponse response);
    void ClearAuthState();
    bool CanUseMode(WorkspaceMode mode);
    bool CanUseInspect();
    int GetMaxTopK();
    int GetMaxCandidatePool();
}
```

**Task 3.3.2:** Create `Services/AuthStateService.cs`. Implement all interface members.

- `IsAuthenticated` returns true if `ApiKey` is not null or empty
- `SessionId` is auto-generated with `Guid.NewGuid().ToString("N")[..12]` on construction
- `SetAuthState` stores ApiKey, Username, Tier from the `AuthResponse`
- `ClearAuthState` nulls ApiKey and Username, resets Tier to "free"
- `CanUseMode(WorkspaceMode mode)`:
  - `free` → only Narrator is allowed (maps to remembrancer endpoint)
  - `adept` → Narrator and Explorer allowed
  - `archmagos` → all modes allowed
- `CanUseInspect()`: returns true only if Tier is "archmagos"
- `GetMaxTopK()`: free=4, adept=8, archmagos=10
- `GetMaxCandidatePool()`: free=10, adept=30, archmagos=50
- Fire `OnAuthStateChanged` whenever state changes

**Task 3.3.3:** Register `AuthStateService` as a **scoped** service in `Program.cs`. Scoped is correct for Blazor Server because each circuit (browser connection) gets its own instance.

### 3.4 — IThemeService Interface

**Task 3.4.1:** Create `Services/Interfaces/IThemeService.cs`:
```csharp
public interface IThemeService
{
    ThemePreset ActivePreset { get; }
    string BackgroundColor { get; }
    string AccentColor { get; }
    string TextColor { get; }
    string FontFamily { get; }
    event Action? OnThemeChanged;
    void ApplyPreset(ThemePreset preset);
    void ApplyModeTheme(WorkspaceMode mode);
    Dictionary<string, string> GetCssVariables();
}
```

**Task 3.4.2:** Create `Services/ThemeService.cs`. Implement:

- `ApplyModeTheme(WorkspaceMode.Narrator)`:
  - Background: `#0d0d0d`
  - Accent: `#c8922a`
  - Text: `#e8dcc8`
  - Font: `Georgia, 'Times New Roman', serif`

- `ApplyModeTheme(WorkspaceMode.Explorer)`:
  - Background: `#12181f`
  - Accent: `#4a9eba`
  - Text: `#c8d8e8`
  - Font: `'Courier New', Courier, monospace`

- `ApplyPreset(ThemePreset preset)`:
  Implement the following presets:
  - `MechanicusRed`: bg `#0f0a0a`, accent `#8b1a1a`, text `#e8c8c8`
  - `ImperialGold`: bg `#0a0907`, accent `#c8a84b`, text `#e8e0c8`
  - `UltramarineBlue`: bg `#060a12`, accent `#1a4a8b`, text `#c8d4e8`
  - `ChaosVoid`: bg `#080408`, accent `#6b1a8b`, text `#d8c8e8`
  - `DarkArchive`: bg `#080808`, accent `#4a4a4a`, text `#c8c8c8`
  - `TacticalSlate`: bg `#0a0f14`, accent `#2a5a6a`, text `#b8c8d8`

- `GetCssVariables()` returns a `Dictionary<string, string>` like:
  ```
  { "--bg-primary": "#0d0d0d", "--accent": "#c8922a", ... }
  ```

- Fire `OnThemeChanged` whenever theme changes

**Task 3.4.3:** Register `ThemeService` as **scoped** in `Program.cs`.

### 3.5 — HttpClient Registration

**Task 3.5.1:** In `Program.cs`, register a named `HttpClient` called `"ForgeApi"`:
```csharp
builder.Services.AddHttpClient("ForgeApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ForgeApi:BaseUrl"] ?? "http://localhost:5000/");
    client.Timeout = TimeSpan.FromSeconds(120);
});
```

**Task 3.5.2:** Add to `appsettings.json`:
```json
"ForgeApi": {
  "BaseUrl": "http://localhost:5000/"
}
```

**Task 3.5.3:** Register `IForgeApiService` → `ForgeApiService` as **scoped** in `Program.cs`.

---

## SECTION 4 — THEME INJECTION INTO BLAZOR

### 4.1 — Theme CSS Variables in App

**Task 4.1.1:** Open `Components/App.razor`. Locate the `<head>` section.

**Task 4.1.2:** Inject `IThemeService` into `App.razor` using `@inject IThemeService ThemeService`.

**Task 4.1.3:** Add a `<style>` block inside `<head>` that outputs CSS variables dynamically:
```razor
<style>
    :root {
        @foreach (var kvp in ThemeService.GetCssVariables())
        {
            <text>@kvp.Key: @kvp.Value;</text>
        }
    }
</style>
```

**Task 4.1.4:** Subscribe to `ThemeService.OnThemeChanged` in `App.razor`'s `OnInitialized` lifecycle method. Call `StateHasChanged()` when theme changes so the CSS variables re-render.

**Task 4.1.5:** Unsubscribe from the event in `IDisposable.Dispose()`. Make `App.razor` implement `IDisposable`.

### 4.2 — Global Base CSS

**Task 4.2.1:** Create `wwwroot/css/forge-base.css`. Add:
```css
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

html, body {
    background-color: var(--bg-primary);
    color: var(--text-primary);
    font-family: var(--font-body);
    height: 100%;
    overflow: hidden;
}

::-webkit-scrollbar { width: 4px; }
::-webkit-scrollbar-track { background: var(--bg-primary); }
::-webkit-scrollbar-thumb { background: var(--accent); border-radius: 2px; }

::selection { background: var(--accent); color: var(--bg-primary); }
```

**Task 4.2.2:** Reference `forge-base.css` in `App.razor` inside `<head>`:
```html
<link rel="stylesheet" href="css/forge-base.css" />
```

---

## SECTION 5 — AUTHENTICATION PAGES

### 5.1 — Login Page

**Task 5.1.1:** Create `Components/Pages/Auth/Login.razor`.

Set page route: `@page "/login"`

Set render mode: `@rendermode InteractiveServer`

Inject: `IForgeApiService`, `IAuthStateService`, `NavigationManager`

**Task 5.1.2:** Add the following C# fields:
```csharp
private string _email = "";
private string _password = "";
private string? _errorMessage;
private bool _isLoading = false;
```

**Task 5.1.3:** Build the UI structure in markup:
- Full-screen centered layout
- A branded header: display the text `THE FORGE` in large spaced letters, subtitle `BLACK OCULUS TERMINAL` below it // [DOMAIN]
- A card/panel containing:
  - Label + text input for Email (type="email", bind to `_email`)
  - Label + text input for Password (type="password", bind to `_password`)
  - An error message area (only visible when `_errorMessage` is not null), styled in red
  - A submit button labeled `ACCESS TERMINAL` // [DOMAIN]
  - A link to `/register` labeled `REQUEST ACCESS` // [DOMAIN]

**Task 5.1.4:** Implement `HandleLogin()` async method:
1. Set `_isLoading = true`, clear `_errorMessage`
2. Call `IForgeApiService.LoginAsync(new LoginRequest { Email = _email, Password = _password })`
3. If response is null: set `_errorMessage = "Authentication failed. Verify credentials."` // [DOMAIN]
4. If response is not null: call `IAuthStateService.SetAuthState(response)`, then `NavigationManager.NavigateTo("/workspace")`
5. Always set `_isLoading = false` in a finally block

**Task 5.1.5:** Wire the submit button to `HandleLogin()`. Disable the button while `_isLoading` is true.

**Task 5.1.6:** Create `Components/Pages/Auth/Login.razor.css`. Style:
- Full-screen dark background using `var(--bg-primary)`
- Centered container, max-width 420px
- Brand title: large, letter-spaced, using `var(--accent)` color
- Input fields: dark background slightly lighter than page, `var(--accent)` border on focus, monospaced font
- Submit button: `var(--accent)` background, dark text, uppercase, letter-spaced, hover glow effect using box-shadow with accent color
- Error message: `#ff4444` color, small font, animated fade-in

### 5.2 — Register Page

**Task 5.2.1:** Create `Components/Pages/Auth/Register.razor`.

Set page route: `@page "/register"`

Set render mode: `@rendermode InteractiveServer`

Inject: `IForgeApiService`, `IAuthStateService`, `NavigationManager`

**Task 5.2.2:** Add fields:
```csharp
private string _email = "";
private string _username = "";
private string _password = "";
private string _confirmPassword = "";
private string? _errorMessage;
private bool _isLoading = false;
```

**Task 5.2.3:** Build UI structure:
- Same branded header as Login page
- Card containing:
  - Email input
  - Username input
  - Password input
  - Confirm Password input
  - Error message area
  - Submit button labeled `FORGE IDENTITY` // [DOMAIN]
  - Link back to `/login` labeled `RETURN TO LOGIN`

**Task 5.2.4:** Implement `HandleRegister()` async method:
1. Validate `_password == _confirmPassword`. If not, set error and return.
2. Validate `_email`, `_username`, `_password` are not empty. If any empty, set error and return.
3. Set `_isLoading = true`, clear error
4. Call `IForgeApiService.RegisterAsync(new RegisterRequest { Email = _email, Username = _username, Password = _password })`
5. On success: call `IAuthStateService.SetAuthState(response)`, navigate to `/workspace`
6. On null: set error message

**Task 5.2.5:** Create `Components/Pages/Auth/Register.razor.css` with the same style conventions as Login.

### 5.3 — Auth Guard

**Task 5.3.1:** Create `Components/Shared/AuthGuard.razor`:
```razor
@inject IAuthStateService AuthState
@inject NavigationManager Nav

@if (AuthState.IsAuthenticated)
{
    @ChildContent
}

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnInitialized()
    {
        if (!AuthState.IsAuthenticated)
            Nav.NavigateTo("/login");
    }
}
```

**Task 5.3.2:** Wrap the `QueryWorkspace` page content in `<AuthGuard>` (implemented in Section 6).

---

## SECTION 6 — QUERY WORKSPACE PAGE

This is the primary and most important component.

### 6.1 — File Setup

**Task 6.1.1:** Create `Components/Pages/Workspace/QueryWorkspace.razor`.

Set page route: `@page "/workspace"`

Set render mode: `@rendermode InteractiveServer`

Inject:
```razor
@inject IForgeApiService ForgeApi
@inject IAuthStateService AuthState
@inject IThemeService ThemeService
@inject NavigationManager Nav
```

**Task 6.1.2:** Create `Components/Pages/Workspace/QueryWorkspace.razor.css`. This file will hold all styles for this page. Leave it empty for now — populate it as each UI element is built.

### 6.2 — Component-Level State Fields

**Task 6.2.1:** Add the following private fields inside the `@code` block:

```csharp
// Mode
private WorkspaceMode _mode = WorkspaceMode.Narrator;

// Query input
private string _queryText = "";
private string _bookFilter = "";
private string? _errorMessage;

// Retrieval controls
private int _topK = 10;
private int _candidatePool = 50;
private int _stitchingWindow = 5;
private bool _advancedControlsVisible = false;

// Streaming state
private StreamState _streamState = StreamState.Idle;
private System.Text.StringBuilder _responseBuffer = new();
private string _responseText = "";
private CancellationTokenSource? _cts;

// Sources
private List<SourceResult> _sources = new();
private bool _sourcesVisible = false;

// Health / system info
private HealthResponse? _health;
private bool _healthLoaded = false;

// Inspect
private bool _inspectPanelVisible = false;
private InspectResponse? _inspectData;
private bool _inspectLoading = false;

// Explorer structured sections
private List<string> _explorerSections = new();
```

### 6.3 — Lifecycle Methods

**Task 6.3.1:** Implement `OnInitializedAsync()`:
1. Check `AuthState.IsAuthenticated`. If false, call `Nav.NavigateTo("/login")` and return.
2. Call `ApplyModeTheme()` for the current `_mode`.
3. Call `await LoadHealthAsync()`.

**Task 6.3.2:** Implement `LoadHealthAsync()`:
1. Call `await ForgeApi.GetHealthAsync()`
2. Store result in `_health`
3. Set `_healthLoaded = true`
4. Call `StateHasChanged()`

**Task 6.3.3:** Implement `ApplyModeTheme()`:
1. Call `ThemeService.ApplyModeTheme(_mode)`
2. Clamp `_topK` to `AuthState.GetMaxTopK()` if it exceeds the max
3. Clamp `_candidatePool` to `AuthState.GetMaxCandidatePool()` if it exceeds the max

**Task 6.3.4:** Implement `SetMode(WorkspaceMode mode)`:
1. If `!AuthState.CanUseMode(mode)`, set `_errorMessage = "Upgrade your access tier to use this mode."` and return
2. Set `_mode = mode`
3. Call `ApplyModeTheme()`
4. Clear `_responseBuffer`, `_responseText`, `_sources`, `_errorMessage`
5. Set `_streamState = StreamState.Idle`
6. Set `_sourcesVisible = false`
7. Call `StateHasChanged()`

### 6.4 — Top Status Bar

**Task 6.4.1:** In the Razor markup, add the top status bar as the first element inside the page wrapper div. It must always be visible.

Structure:
```
[health dot] [model name] [chunk count] | [mode label] | [session id (short)]
```

**Task 6.4.2:** The health dot is a `<span>` with CSS class `status-dot`. Apply additional class `status-dot--online` if `_health?.RetrieverReady == true`, else `status-dot--offline`.

**Task 6.4.3:** Display `_health?.Model ?? "—"` for model name.

**Task 6.4.4:** Display `_health?.ChunksLoaded?.ToString("N0") ?? "—"` + ` chunks` for chunk count.

**Task 6.4.5:** Display current mode label. Use a computed property:
```csharp
private string ModeLabelText => _mode switch
{
    WorkspaceMode.Narrator => "NARRATOR", // [DOMAIN]
    WorkspaceMode.Explorer => "EXPLORER", // [DOMAIN]
    _ => "UNKNOWN"
};
```

**Task 6.4.6:** Display `AuthState.SessionId[..8]` as the short session ID.

**Task 6.4.7:** Add a pulse animation class `status-bar--streaming` to the top bar's container div when `_streamState == StreamState.Streaming`. This adds a slow amber glow pulse in Narrator mode.

**Task 6.4.8:** In `QueryWorkspace.razor.css`, style the status bar:
- Fixed to top, full width, height 40px
- Background slightly lighter than page bg: use `rgba(255,255,255,0.03)`
- Border bottom: `1px solid var(--accent)` at 30% opacity
- Flex row, items centered, padding 0 16px, gap 16px
- Font: monospaced, size 11px, letter-spacing 1px, uppercase
- `.status-dot`: 8px circle
- `.status-dot--online`: background `#00ff88`
- `.status-dot--offline`: background `#ff4444`
- `.status-bar--streaming`: add keyframe animation `pulse-glow` that pulses `box-shadow` with `var(--accent)` at 0.3 opacity, period 2s infinite

### 6.5 — Mode Selector

**Task 6.5.1:** Below the status bar, add the mode selector. It is a row of toggle buttons.

Buttons (only show modes the current tier can use):
- `NARRATOR` — calls `SetMode(WorkspaceMode.Narrator)`
- `EXPLORER` — calls `SetMode(WorkspaceMode.Explorer)`

**Task 6.5.2:** The active mode button gets class `mode-btn--active`. Inactive buttons get class `mode-btn--inactive`.

**Task 6.5.3:** In `QueryWorkspace.razor.css`, style mode buttons:
- No background on inactive, border: `1px solid var(--accent)` at 40% opacity
- Active: background `var(--accent)` at 15%, border `1px solid var(--accent)`, text `var(--accent)`
- Font: monospaced, uppercase, letter-spacing 2px, size 12px
- Padding 8px 20px
- Hover: background `var(--accent)` at 8%
- Transition: all 0.2s ease

### 6.6 — Query Input Area

**Task 6.6.1:** Add a textarea below the mode selector. Bind it to `_queryText` with `@bind`.

**Task 6.6.2:** Set the placeholder dynamically based on mode using a computed property:
```csharp
private string QueryPlaceholder => _mode switch
{
    // [DOMAIN] All placeholder strings below are Warhammer-specific
    WorkspaceMode.Narrator => "Request a scene, an event, a chronicle, or ask why something happened...",
    WorkspaceMode.Explorer => "Name an object, weapon, artifact, or entity for analysis...",
    _ => "Ask a question..."
};
```

**Task 6.6.3:** Add a row below the textarea with:
- Left side: a `SUBMIT` button (or mode-appropriate label: `CHRONICLE` for Narrator, `ANALYSE` for Explorer) // [DOMAIN]
- Right side (only during streaming): a `STOP` button styled in red/dark-red
- Below the buttons: error message display (only when `_errorMessage` is not null)

**Task 6.6.4:** The submit button is disabled when:
- `_queryText.Trim().Length == 0`
- `_streamState == StreamState.Streaming`

**Task 6.6.5:** In `QueryWorkspace.razor.css`, style the textarea:
- Width 100%, min-height 80px, max-height 200px
- Background: `rgba(255,255,255,0.03)`
- Border: `1px solid var(--accent)` at 25% opacity
- Border-radius: 4px
- Color: `var(--text-primary)`
- Font: inherited from mode (this means serif in Narrator, monospace in Explorer)
- Padding 12px 16px
- Focus border: `var(--accent)` at 70%
- Resize: vertical only
- No outline on focus (use border instead)

**Task 6.6.6:** Style the submit button:
- Background: `var(--accent)` at 90%
- Text: dark (near-black)
- Uppercase, letter-spaced, bold
- Padding 10px 28px
- Hover: background 100% accent, slight box-shadow glow
- Disabled: opacity 0.3, cursor not-allowed

**Task 6.6.7:** Style the STOP button:
- Background: transparent
- Border: `1px solid #ff4444`
- Text: `#ff4444`
- Same sizing as submit
- Hover: background `rgba(255,68,68,0.1)`

### 6.7 — Advanced Controls Panel

**Task 6.7.1:** Below the query input row, add an `ADVANCED CONTROLS` toggle button. Clicking it flips `_advancedControlsVisible`.

**Task 6.7.2:** When `_advancedControlsVisible` is true, render a panel containing:

1. **top_k slider:**
   - Label: `DEPTH: {_topK}`
   - `<input type="range">` bound to `_topK`
   - Min: 1, Max: `AuthState.GetMaxTopK()`, Step: 1
   - On change: clamp value

2. **candidate_pool slider:**
   - Label: `POOL: {_candidatePool}`
   - Min: 10, Max: `AuthState.GetMaxCandidatePool()`, Step: 5

3. **stitching_window slider:**
   - Label: `STITCH: {_stitchingWindow}`
   - Min: 0, Max: 6, Step: 1

4. **session_id display:**
   - Label: `SESSION`
   - Display full `AuthState.SessionId` in a readonly monospaced text box
   - A small COPY button next to it (uses `IJSRuntime` only if no native Blazor alternative — if using JS, isolate it)

5. **book_filter input:**
   - Label: `FILTER BY BOOK` // [DOMAIN]
   - Text input bound to `_bookFilter`
   - Placeholder: `e.g. Betrayer, Fulgrim...` // [DOMAIN]

6. **Clear Memory button:**
   - Label: `CLEAR MEMORY`
   - Calls `await ForgeApi.DeleteMemoryAsync(AuthState.SessionId, AuthState.ApiKey!)`
   - On click: set `_errorMessage = null`, show brief feedback "Memory cleared" // [DOMAIN]

**Task 6.7.3:** In `QueryWorkspace.razor.css`, style the advanced panel:
- Subtle bordered box, `rgba(255,255,255,0.02)` background
- Padding 16px
- Grid layout: 3 columns on desktop, 1 column on mobile
- Slider track: thin, accent-colored thumb
- Labels: 10px, uppercase, letter-spacing 1px, `var(--accent)` color at 70%

### 6.8 — Response Panel

**Task 6.8.1:** Below the query area, add the response panel. This is the largest element on the page.

Apply CSS class based on mode:
- Narrator mode: class `response-panel response-panel--narrator`
- Explorer mode: class `response-panel response-panel--explorer`

**Task 6.8.2:** Handle each stream state differently:

**Idle state** (`StreamState.Idle`):
- Show placeholder text appropriate to the mode
- Narrator idle: subtle centered text — `"The archive awaits your query..."` // [DOMAIN]
- Explorer idle: subtle centered text — `"Input a designation for analysis..."` // [DOMAIN]
- Style: centered, low opacity (0.3), italic

**Streaming state** (`StreamState.Streaming`):
- Show `_responseText` (bound to `_responseBuffer.ToString()`)
- Narrator: typewriter cursor blinking at the end — add a `<span class="cursor">` element after the text
- Explorer: no cursor, just text appearing

**Complete state** (`StreamState.Complete`):
- Show `_responseText`
- Narrator: fade in a completion indicator (small `■` at the end) // [DOMAIN]
- Explorer: render response sections (see Task 6.8.5)

**Error state** (`StreamState.Error`):
- Show `_errorMessage` in red

**Cancelled state** (`StreamState.Cancelled`):
- Show `_responseText` with a dim label `[STREAM INTERRUPTED]` at the end

**Task 6.8.3:** In `QueryWorkspace.razor.css`, style the response panel:

Common styles:
- Min-height 300px, flex-grow to fill available space
- Padding 24px
- Overflow-y auto
- Border: `1px solid var(--accent)` at 15% opacity
- Border-radius 4px
- Background: `rgba(255,255,255,0.015)`

Narrator specific (`.response-panel--narrator`):
- Font: `Georgia, serif`
- Font-size: 16px
- Line-height: 1.8
- Letter-spacing: 0.01em

Explorer specific (`.response-panel--explorer`):
- Font: `'Courier New', monospace`
- Font-size: 14px
- Line-height: 1.6
- Letter-spacing: 0.02em

Cursor:
```css
.cursor {
    display: inline-block;
    width: 2px;
    height: 1em;
    background: var(--accent);
    margin-left: 2px;
    vertical-align: text-bottom;
    animation: blink 1s step-end infinite;
}
@keyframes blink { 50% { opacity: 0; } }
```

**Task 6.8.4:** For Explorer mode, implement section parsing. After response is complete, call `ParseExplorerSections()`:
```csharp
private void ParseExplorerSections()
{
    _explorerSections = _responseText
        .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
        .Where(s => s.Trim().Length > 0)
        .ToList();
}
```

Render each section in Explorer mode as:
```razor
@foreach (var section in _explorerSections)
{
    <div class="explorer-section">
        <p>@section</p>
    </div>
}
```

**Task 6.8.5:** In `QueryWorkspace.razor.css`, style Explorer sections:
- Border-left: `3px solid var(--accent)` at 60%
- Padding-left: 16px
- Margin-bottom: 24px
- First line of each section: slightly larger, `var(--accent)` color (use `:first-line` pseudo-element)

### 6.9 — Streaming Implementation

**Task 6.9.1:** Implement `HandleSubmit()` async method:

```csharp
private async Task HandleSubmit()
{
    if (string.IsNullOrWhiteSpace(_queryText)) return;
    if (_streamState == StreamState.Streaming) return;

    // Reset state
    _responseBuffer.Clear();
    _responseText = "";
    _sources.Clear();
    _sourcesVisible = false;
    _explorerSections.Clear();
    _errorMessage = null;
    _inspectData = null;
    _streamState = StreamState.Idle;

    var request = BuildQueryRequest();

    if (_mode == WorkspaceMode.Explorer)
    {
        await HandleExplorerQuery(request);
    }
    else
    {
        await HandleNarratorStream(request);
    }
}
```

**Task 6.9.2:** Implement `BuildQueryRequest()`:
```csharp
private QueryRequest BuildQueryRequest()
{
    return new QueryRequest
    {
        Query = _queryText.Trim(),
        SessionId = AuthState.SessionId,
        TopK = _topK,
        CandidatePool = _candidatePool,
        StitchingWindow = _stitchingWindow,
        BookFilter = string.IsNullOrWhiteSpace(_bookFilter) ? null : _bookFilter.Trim()
    };
}
```

**Task 6.9.3:** Implement `HandleNarratorStream(QueryRequest request)`:

```csharp
private async Task HandleNarratorStream(QueryRequest request)
{
    _cts = new CancellationTokenSource();
    _streamState = StreamState.Streaming;
    StateHasChanged();

    try
    {
        var response = await ForgeApi.BeginNarrateStreamAsync(request, AuthState.ApiKey!, _cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = $"Request failed: {response.StatusCode}";
            _streamState = StreamState.Error;
            StateHasChanged();
            return;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !_cts.Token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            if (!line.StartsWith("data:")) continue;

            var data = line["data:".Length..].Trim();

            if (data == "[DONE]")
            {
                _streamState = StreamState.Complete;
                if (_mode == WorkspaceMode.Explorer) ParseExplorerSections();
                break;
            }

            if (data.StartsWith("[ERROR]"))
            {
                _errorMessage = data["[ERROR]".Length..].Trim();
                _streamState = StreamState.Error;
                break;
            }

            if (data.StartsWith("__SOURCES__:"))
            {
                var json = data["__SOURCES__:".Length..];
                try
                {
                    _sources = System.Text.Json.JsonSerializer.Deserialize<List<SourceResult>>(json,
                        new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new();
                    _sourcesVisible = true;
                }
                catch { /* malformed sources — ignore silently */ }
            }
            else
            {
                _responseBuffer.Append(data);
                _responseText = _responseBuffer.ToString();
            }

            StateHasChanged();
        }

        if (_cts.Token.IsCancellationRequested && _streamState == StreamState.Streaming)
        {
            _streamState = StreamState.Cancelled;
        }
        else if (_streamState == StreamState.Streaming)
        {
            _streamState = StreamState.Complete;
        }
    }
    catch (OperationCanceledException)
    {
        _streamState = StreamState.Cancelled;
    }
    catch (Exception ex)
    {
        _errorMessage = $"Stream error: {ex.Message}";
        _streamState = StreamState.Error;
    }
    finally
    {
        _cts?.Dispose();
        _cts = null;
        StateHasChanged();
    }
}
```

**Task 6.9.4:** Implement `HandleExplorerQuery(QueryRequest request)`:
```csharp
private async Task HandleExplorerQuery(QueryRequest request)
{
    _streamState = StreamState.Streaming; // reuse Streaming for loading indicator
    StateHasChanged();

    try
    {
        var result = await ForgeApi.QueryExploreAsync(request, AuthState.ApiKey!);

        if (result == null)
        {
            _errorMessage = "Analysis failed. The archive returned no data."; // [DOMAIN]
            _streamState = StreamState.Error;
            return;
        }

        _responseText = result.Response;
        _responseBuffer.Append(result.Response);
        _sources = result.Sources;
        _sourcesVisible = _sources.Count > 0;
        ParseExplorerSections();
        _streamState = StreamState.Complete;
    }
    catch (Exception ex)
    {
        _errorMessage = $"Error: {ex.Message}";
        _streamState = StreamState.Error;
    }
    finally
    {
        StateHasChanged();
    }
}
```

**Task 6.9.5:** Implement `StopStream()`:
```csharp
private void StopStream()
{
    _cts?.Cancel();
}
```

Wire the STOP button's `@onclick` to `StopStream`.

### 6.10 — Sources Panel

**Task 6.10.1:** Below the response panel, add the sources panel. It is only visible when `_sourcesVisible` is true.

Structure:
- Header: `SOURCES` label with a count badge `({_sources.Count})`
- A list of source items

**Task 6.10.2:** Each source item shows:
- Source filename (display only the last part after any `/`): `Path.GetFileName(source.Source)`
- Chapter if not "unknown": `Chapter @source.Chapter`
- Stitch range if not null: `Chunks @source.StitchRange`
- Confidence bar (NOT the raw score number):
  - A `<div class="confidence-bar">` containing a `<div class="confidence-fill">` with inline style `width: @(Math.Min(source.Score * 100, 100))%`
  - This is the only place inline style is acceptable — it's data-driven width

**Task 6.10.3:** In `QueryWorkspace.razor.css`, style the sources panel:

Narrator mode appearance:
- Slides in from bottom — use CSS transition on `max-height` from 0 to 300px
- Background: `rgba(200, 146, 42, 0.05)` (amber tint)
- Border-top: `1px solid var(--accent)` at 30%

Explorer mode appearance:
- Appears below response, no slide animation
- Background: `rgba(74, 158, 186, 0.05)` (steel tint)
- Clean ranked list style

Confidence bar:
```css
.confidence-bar {
    height: 3px;
    background: rgba(255,255,255,0.1);
    border-radius: 2px;
    margin-top: 4px;
    width: 100%;
}
.confidence-fill {
    height: 100%;
    background: var(--accent);
    border-radius: 2px;
    transition: width 0.5s ease;
}
```

### 6.11 — Inspect Panel (Admin/Dev Only)

**Task 6.11.1:** In the bottom-right corner of the page, add a small `DEV` button. Only visible if `AuthState.CanUseInspect()` returns true.

Clicking it toggles `_inspectPanelVisible`.

**Task 6.11.2:** When `_inspectPanelVisible` is true and a query has been submitted (response is complete), show an async-loaded inspect panel.

Add a button inside the panel: `RUN INSPECT`. Clicking it calls `HandleInspectQuery()`.

**Task 6.11.3:** Implement `HandleInspectQuery()`:
```csharp
private async Task HandleInspectQuery()
{
    if (string.IsNullOrWhiteSpace(_queryText)) return;
    _inspectLoading = true;
    StateHasChanged();

    try
    {
        _inspectData = await ForgeApi.QueryInspectAsync(BuildQueryRequest(), AuthState.ApiKey!);
    }
    finally
    {
        _inspectLoading = false;
        StateHasChanged();
    }
}
```

**Task 6.11.4:** Render `_inspectData` when it is not null:

For each hit list (FAISS, BM25, Grounded, Stitched):
- Section header with the list name
- Each hit renders:
  - Source filename
  - Score: a horizontal bar, green (`#00ff88`) if score > 0, red (`#ff4444`) if score < 0
    - Width as percentage of a max (normalize: `Math.Clamp(Math.Abs(score) * 100, 0, 100)`)
  - Text preview (first 120 chars of `TextPreview`)
  - Highlight any words in `_inspectData.QueryTerms` found in the preview text using `<mark>` tags

**Task 6.11.5:** Implement a helper that highlights query terms in a text string:
```csharp
private string HighlightTerms(string text, List<string> terms)
{
    foreach (var term in terms)
    {
        if (string.IsNullOrWhiteSpace(term)) continue;
        text = System.Text.RegularExpressions.Regex.Replace(
            text,
            System.Text.RegularExpressions.Regex.Escape(term),
            $"<mark>{term}</mark>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
    }
    return text;
}
```

Render the highlighted preview using `@((MarkupString)HighlightTerms(hit.TextPreview ?? "", _inspectData?.QueryTerms ?? new()))`.

**Task 6.11.6:** In `QueryWorkspace.razor.css`, style the inspect panel:
```css
.inspect-panel {
    background: #0a0a0a;
    color: #00ff41;
    font-family: 'Courier New', monospace;
    font-size: 11px;
    padding: 16px;
    border: 1px solid #00ff41;
    border-radius: 4px;
    margin-top: 16px;
    max-height: 400px;
    overflow-y: auto;
}
.inspect-panel mark {
    background: transparent;
    color: #c8922a;
    font-weight: bold;
}
.score-bar-positive { background: #00ff88; }
.score-bar-negative { background: #ff4444; }
```

### 6.12 — Memory Controls Footer

**Task 6.12.1:** At the bottom of the page, add a fixed footer with:
- Left: `SESSION: {AuthState.SessionId[..8]}...`
- Center: Memory turn count label — call `GetMemoryAsync` on load and display count (or just show last 4 turns note)
- Right: `CLEAR MEMORY` button — calls `await ForgeApi.DeleteMemoryAsync(AuthState.SessionId, AuthState.ApiKey!)`

**Task 6.12.2:** After `DeleteMemoryAsync` completes, show a brief status message `Memory purged` that fades out after 2 seconds.

Implement fade-out with a CSS class that is applied and then removed via `Task.Delay(2000)`:
```csharp
private bool _memoryCleared = false;
private async Task ClearMemory()
{
    await ForgeApi.DeleteMemoryAsync(AuthState.SessionId, AuthState.ApiKey!);
    _memoryCleared = true;
    StateHasChanged();
    await Task.Delay(2000);
    _memoryCleared = false;
    StateHasChanged();
}
```

**Task 6.12.3:** Style the footer in `QueryWorkspace.razor.css`:
- Fixed to bottom, full width, height 36px
- Background same as status bar top
- Border-top: `1px solid var(--accent)` at 20%
- Font: 10px, monospaced, uppercase
- Flex row, space-between

---

## SECTION 7 — LAYOUT

### 7.1 — ForgeLayout

**Task 7.1.1:** Open (or create) `Components/Layout/ForgeLayout.razor`.

This layout is used by the workspace page. It removes the default navigation and provides only the page body.

Structure:
```razor
@inherits LayoutComponentBase
@inject IThemeService ThemeService

<div class="forge-layout">
    @Body
</div>
```

**Task 7.1.2:** Create `Components/Layout/ForgeLayout.razor.css`:
```css
.forge-layout {
    display: flex;
    flex-direction: column;
    height: 100vh;
    overflow: hidden;
    background: var(--bg-primary);
    color: var(--text-primary);
    font-family: var(--font-body);
}
```

**Task 7.1.3:** In `QueryWorkspace.razor`, set the layout:
```razor
@layout ForgeLayout
```

**Task 7.1.4:** Create a separate `AuthLayout.razor` for login and register pages. It should be full-screen, centered, dark, no navigation.

### 7.2 — Apply Layouts to Pages

**Task 7.2.1:** Add `@layout AuthLayout` to `Login.razor` and `Register.razor`.

**Task 7.2.2:** Add `@layout ForgeLayout` to `QueryWorkspace.razor`.

---

## SECTION 8 — NAVIGATION AND ROUTING

**Task 8.1:** Open `Components/Routes.razor`. Ensure the router is configured to use `ForgeLayout` as the default layout or that pages declare their own layout.

**Task 8.2:** Add a redirect: if user navigates to `/`, redirect to `/workspace` if authenticated, else redirect to `/login`. Implement this in a minimal `Index.razor` at `/`:
```razor
@page "/"
@inject IAuthStateService AuthState
@inject NavigationManager Nav
@code {
    protected override void OnInitialized()
    {
        Nav.NavigateTo(AuthState.IsAuthenticated ? "/workspace" : "/login");
    }
}
```

**Task 8.3:** Add a logout mechanism. In the status bar or footer, add a small `LOGOUT` link. Clicking it:
1. Calls `AuthState.ClearAuthState()`
2. Navigates to `/login`

---

## SECTION 9 — SETTINGS PAGE (SCAFFOLD ONLY — DO NOT WIRE YET)

**Task 9.1:** Create `Components/Pages/Settings/Settings.razor` at `@page "/settings"`.

Add `@layout ForgeLayout`.

Add placeholder content:
```razor
<div class="settings-page">
    <h1 class="settings-title">FORGE SETTINGS</h1> <!-- [DOMAIN] -->
    <p class="settings-placeholder">Configuration modules loading...</p>
    <!-- TODO: Wire settings from /api/settings in Phase 2 -->
</div>
```

**Task 9.2:** Create `Components/Pages/Settings/Settings.razor.css` with minimal dark styling matching the workspace theme.

**Task 9.3:** Add a settings icon/link in the status bar that navigates to `/settings`.

---

## SECTION 10 — PROGRAM.CS REGISTRATION CHECKLIST

**Task 10.1:** Open `Program.cs` and verify or add each of the following registrations. Add them if missing:

1. `builder.Services.AddRazorComponents().AddInteractiveServerComponents();`
2. `builder.Services.AddHttpClient("ForgeApi", ...)` — as specified in Section 3.5
3. `builder.Services.AddScoped<IForgeApiService, ForgeApiService>();`
4. `builder.Services.AddScoped<IAuthStateService, AuthStateService>();`
5. `builder.Services.AddScoped<IThemeService, ThemeService>();`

**Task 10.2:** Verify `app.MapRazorComponents<App>().AddInteractiveServerRenderMode();` is present in the middleware pipeline.

**Task 10.3:** Verify `ForgeAuthMiddleware` is still registered and that it does NOT block `/login`, `/register`, or `/api/health` — these must remain public.

---

## SECTION 11 — RESPONSIVE DESIGN

**Task 11.1:** In `QueryWorkspace.razor.css`, add a media query for screens under 768px:
- Stack mode selector vertically
- Set textarea height to 60px minimum
- Collapse advanced controls automatically (`_advancedControlsVisible = false` on load for mobile — detect via JS interop ONLY if necessary, otherwise default to collapsed)
- Make sources panel full-width below the response
- Status bar: show only health dot and mode label on mobile (hide model name and session ID)

**Task 11.2:** In `Login.razor.css` and `Register.razor.css`, add media query for mobile:
- Remove card drop shadow
- Full-width card (padding 16px)

---

## SECTION 12 — LOADING STATES AND ANIMATIONS

**Task 12.1:** For the Explorer mode loading state (while `_streamState == StreamState.Streaming`), add a scanning bar animation in `QueryWorkspace.razor.css`:
```css
@keyframes scan-line {
    0% { transform: translateX(-100%); }
    100% { transform: translateX(100%); }
}
.scan-loading {
    height: 2px;
    background: linear-gradient(90deg, transparent, var(--accent), transparent);
    animation: scan-line 1.5s ease-in-out infinite;
    width: 100%;
    margin-bottom: 8px;
}
```

Display `<div class="scan-loading"></div>` above the response panel when in Explorer mode and `_streamState == StreamState.Streaming`.

**Task 12.2:** For the Narrator mode streaming state, add a pulse animation on the top bar's border:
```css
@keyframes pulse-glow {
    0%, 100% { box-shadow: 0 0 4px rgba(200, 146, 42, 0.2); }
    50% { box-shadow: 0 0 12px rgba(200, 146, 42, 0.5); }
}
```

---

## SECTION 13 — ERROR HANDLING

**Task 13.1:** Create a `Components/Shared/ErrorBoundary.razor` wrapper if Blazor's built-in `<ErrorBoundary>` is not already used. Wrap the entire workspace page body in an error boundary so uncaught exceptions don't crash the circuit.

**Task 13.2:** In every `catch` block in `QueryWorkspace.razor`, never rethrow. Always:
1. Set `_errorMessage` to a user-friendly string
2. Set `_streamState = StreamState.Error`
3. Call `StateHasChanged()`
4. Log the exception using the injected `ILogger<QueryWorkspace>` (inject it)

**Task 13.3:** Add `@inject ILogger<QueryWorkspace> Logger` to `QueryWorkspace.razor`.

---

## SECTION 14 — SECURITY CHECKLIST

**Task 14.1:** Confirm the API key is stored only in `AuthStateService` (in-memory, scoped to the SignalR circuit). It must NOT be written to `localStorage`, `sessionStorage`, or any persistent storage from the Blazor side.

**Task 14.2:** Confirm no API key or password appears in any Razor markup binding or URL parameter.

**Task 14.3:** Confirm that `HighlightTerms()` output rendered via `@((MarkupString)...)` only receives text that originated from the server response — never from raw user input directly.

**Task 14.4:** Confirm all `HttpClient` calls include the `X-Api-Key` header only when the endpoint requires auth. Public endpoints (`/api/health`, `/api/sources`, `/api/auth/*`) do not send the key.

---

## SECTION 15 — FINAL WIRING AND SMOKE TEST CHECKLIST

Work through this checklist after all sections are complete:

**Task 15.1:** Build the solution. Fix any compile errors. Do not suppress warnings — fix them.

**Task 15.2:** Run the app. Navigate to `http://localhost:{port}/`. Confirm redirect to `/login`.

**Task 15.3:** Register a test user. Confirm redirect to `/workspace`.

**Task 15.4:** In the workspace, confirm the health dot shows status from the API.

**Task 15.5:** Select Narrator mode. Type a query. Submit. Confirm tokens stream into the response panel one by one.

**Task 15.6:** Confirm the STOP button appears during streaming and cancels it.

**Task 15.7:** Confirm sources panel slides in when `__SOURCES__` is received.

**Task 15.8:** Switch to Explorer mode. Confirm theme changes. Submit a query. Confirm sections render.

**Task 15.9:** Open advanced controls. Adjust sliders. Submit a query. Confirm the new values are sent in the request body (verify with browser devtools network tab).

**Task 15.10:** Click DEV button (if archmagos tier). Run inspect on a query. Confirm hits render with score bars.

**Task 15.11:** Click CLEAR MEMORY. Confirm the API is called and feedback message appears and fades.

**Task 15.12:** Log out. Confirm redirect to `/login` and auth state is cleared.

---

## SECTION 16 — PROGRESS TRACKING

At the end of each coding session, update a file called `_progress.md` in the project root.

Use this format:

```markdown
# TheForge — Black Oculus Build Progress

## Last Session: [DATE]
## Completed Sections: [list]
## In Progress: [current section and task number]
## Remaining Sections: [list]
## Known Issues: [list any bugs or incomplete tasks]
## Next Step: [exact task number to pick up from]
```

This file is your recovery document. When the token limit hits mid-session, start the next session by feeding this file so work can resume without regression.

---

## APPENDIX A — FILE REFERENCE MAP

```
TheForge/
├── Components/
│   ├── App.razor                          # Theme CSS injection, head setup
│   ├── Routes.razor                       # Router
│   ├── _Imports.razor                     # Global using directives
│   ├── Layout/
│   │   ├── ForgeLayout.razor              # Workspace layout
│   │   ├── ForgeLayout.razor.css
│   │   ├── AuthLayout.razor               # Login/register layout
│   │   └── AuthLayout.razor.css
│   ├── Pages/
│   │   ├── Index.razor                    # Root redirect
│   │   ├── Auth/
│   │   │   ├── Login.razor
│   │   │   ├── Login.razor.css
│   │   │   ├── Register.razor
│   │   │   └── Register.razor.css
│   │   ├── Workspace/
│   │   │   ├── QueryWorkspace.razor       # PRIMARY PAGE
│   │   │   └── QueryWorkspace.razor.css
│   │   ├── Settings/
│   │   │   ├── Settings.razor             # Scaffold only
│   │   │   └── Settings.razor.css
│   │   └── Admin/                         # Reserved for future
│   └── Shared/
│       ├── AuthGuard.razor
│       └── ErrorBoundary.razor
├── Models/
│   ├── Api/
│   │   ├── QueryRequest.cs
│   │   ├── QueryResponse.cs
│   │   ├── SourceResult.cs
│   │   ├── InspectResponse.cs
│   │   ├── HitResult.cs
│   │   ├── LoginRequest.cs
│   │   ├── RegisterRequest.cs
│   │   ├── AuthResponse.cs
│   │   ├── HealthResponse.cs
│   │   ├── SourceInfo.cs
│   │   └── MemoryTurn.cs
│   └── UI/
│       ├── WorkspaceMode.cs
│       ├── StreamState.cs
│       └── ThemePreset.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IForgeApiService.cs
│   │   ├── IAuthStateService.cs
│   │   └── IThemeService.cs
│   ├── ForgeApiService.cs
│   ├── AuthStateService.cs
│   └── ThemeService.cs
├── wwwroot/
│   └── css/
│       └── forge-base.css
├── Program.cs                             # Service registration, middleware
├── appsettings.json                       # ForgeApi:BaseUrl config
├── _structure.md                          # Auto-generated folder docs
└── _progress.md                          # Session recovery document
```

---

## APPENDIX B — DOMAIN SWAP GUIDE

Every place in the codebase marked with `// [DOMAIN]` must be updated when switching from Warhammer 40K to a different RAG domain. Specifically:

- `WorkspaceMode` enum values (Narrator, Explorer)
- `ThemePreset` enum values (all faction-named presets)
- All placeholder strings in the query textarea
- Idle state text in the response panel
- Error message strings referencing "archive" or "lore"
- Status bar labels
- Auth page brand copy (`THE FORGE`, `BLACK OCULUS TERMINAL`, etc.)
- `CHRONICLE`, `ANALYSE` button labels
- `book_filter` label and placeholder text
- Memory clear feedback text

A clean domain swap requires only string changes — no logic changes.

---

## APPENDIX C — KNOWN FORGE API CONTRACT ISSUES TO WATCH FOR

These are existing mismatches between TheForge .NET API and OmnissiahCore. The Blazor UI does not need to fix them, but it must handle them gracefully:

1. **SourceFilter is `string?` in .NET but FastAPI expects `List<string>`** — the Blazor UI sends `source_filter` as null or omits it. Do not rely on source filtering working correctly until this is fixed server-side.

2. **Streaming `__SOURCES__` frames may be dropped** by `FastApiClient.cs` in some versions — if sources panel never appears, this is the cause. Handle gracefully (sources are optional, not required for the UI to function).

3. **Ollama errors arrive as HTTP 200 with response text starting `[ERROR]`** — the stream parser already handles this (Task 6.9.3), but be aware that a non-2xx status code is not the only error condition.

4. **`/api/query/inspect` may not exist** in the current TheForge routing — confirm in `Controllers/QueryController.cs` before wiring the inspect panel.

---

*END OF PROMPT — FEED ENTIRE DOCUMENT TO CODING ASSISTANT*
*Begin at Section 0. Complete each task in order. Drop CHECKPOINT comments in code.*
*Recovery: read `_progress.md` to resume after token expiry.*
