# TheForge

.NET 8 Blazor Server UI and HTTP gateway for the FastAPI retrieval backend, with a separate read-only SQLite scene archive.

## Structure

```text
Features/
  Workspace/       Query page, UI components, state, query orchestration and SSE parsing
  SceneArchive/    Archive page, components, models, services and read-only data access
  Settings/        Settings page and styles
Components/       App shell, layouts and shared controls
Controllers/      Public HTTP gateway routes
Services/         Shared HTTP transports, theme and query cache
Models/           Shared HTTP contracts and theme models
Tests/TheForge.Tests/
```

Each feature owns its Razor files and isolated CSS. Shared controls own their own CSS. `_Imports.razor` provides common Razor imports; feature-specific imports belong inside the feature.

## Run

For the local frontend connected to the current Oracle API, double-click
**Start TheForge.cmd** in the project root, then use http://127.0.0.1:5157/.
Codex does not need to be open. **Stop TheForge.cmd** stops only this local
frontend. The launcher builds the app, checks Oracle connectivity, and opens
your normal browser. No SSH tunnel or Node test console is required.

In Rider, select the **oracle-local** launch profile. From PowerShell:

```powershell
dotnet run --project TheForge.csproj --launch-profile oracle-local
```

Explorer uses `/query/explore`, Narrator Focus uses `/query/focus`, and Narrator
Wide Net streams `/query/narrate`. Text and source metadata come from Oracle.
The local frontend sends the ngrok interstitial-skip header. The current public
API address is configured in the profile and launcher; update both if that
address changes. A working connection does not establish scene-answer accuracy.

The existing general development profile remains available:

```powershell
dotnet run --project TheForge.csproj --launch-profile http
```

Configure `FastApi:BaseUrl` and `ConnectionStrings:SceneArchiveDb` in configuration or environment variables (`FastApi__BaseUrl`, `ConnectionStrings__SceneArchiveDb`). The SQLite archive is supplied by the Python pipeline and is not migrated by this app. The default listener is port 5157; `ASPNETCORE_URLS` or the launch profile can override it.

## Workspace ownership

- `QueryWorkspace.razor` composes the screen. Its partial class owns page lifetime, initial loading, mode changes, and request cancellation.
- `WorkspaceState` holds one page's selections, filters and results. It is created per page, rather than registered as shared application state.
- `WorkspaceQueryService` selects Explore, Focus or streamed Narrator execution and translates outcomes into state changes.
- `QueryStreamParser` reads SSE events, preserving token whitespace and handling stages, source metadata, completion, cancellation and malformed source frames.
- `ForgeApiService` handles HTTP and JSON. It propagates failures; the page/service logs them and chooses the user-facing message.

Keep UI rendering in components, query execution in the service, and wire-format parsing in the parser. Existing `/`, `/query`, `/archive`, `/settings` and `/api/*` routes remain intact.

## Verify

```powershell
dotnet build TheForge.csproj
dotnet test Tests/TheForge.Tests/TheForge.Tests.csproj
```

The test project includes NUnit service/parser tests and Chromium browser tests. Install the matching Playwright browser once if needed:

```powershell
pwsh Tests/TheForge.Tests/bin/Debug/net8.0/playwright.ps1 install chromium
```

Browser tests launch an isolated app on a free localhost port with a local fake backend, an empty test archive and ephemeral data-protection keys. They cover UI interactions, filters, all query modes, cancellation, responsive CSS and moved static assets. They do not verify live backend retrieval or answer accuracy. Screenshots and app logs are written into the test build output.

`doc/` also contains upstream OmnissiahCore backend reference material; it does not describe this project's directory structure.

## Publish the website

Publish in Rider using **Release** to a folder, or run:

```powershell
dotnet publish TheForge.csproj -c Release --no-self-contained -o bin/publish/site
```

Upload the contents of that publish folder to the site's application root, including
`wwwroot` and `TheForge.dll` from the same publish. Uploading the project source folder
or only the DLL can leave the HTML and Blazor component styles out of sync. Preserve
the host's database and private configuration. Local tools, backend checkouts and
development settings are excluded from the publish output.

Stylesheet URLs include a hash of their contents. When CSS changes, the URL changes
automatically so browsers fetch the matching styles. Ctrl+Shift+R bypasses a browser's
cached files when diagnosing an older deployment.

To check a running published build (not `dotnet run`), set its URL and run the opt-in check:

```powershell
$env:THEFORGE_STYLE_CHECK_URL = 'http://127.0.0.1:5157/'
dotnet test Tests/TheForge.Tests/TheForge.Tests.csproj --filter FullyQualifiedName~PublishedStylesTests
```

It checks stylesheet responses and content hashes, actual applied fonts/layout, the
logo and Scene Archive. It submits no lore queries.
