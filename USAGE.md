# WikiGraph Usage Guide

## What this project contains

- `WikiGraph.Api`: ASP.NET Core REST API with SQLite persistence.
- `WikiGraph.Client`: Blazor WebAssembly UI.
- `WikiGraph.Web`: Additional Blazor UI shell in the repo, kept buildable alongside the WASM client.
- `WikiGraph.Contracts`: Shared DTOs used by the API, UI, and tests.
- `WikiGraph.Tests`: API and persistence tests.

The API now follows a layered structure:

- `Controllers`: REST endpoints
- `Application/Services`: orchestrator, summarizer, graph builder, ingestion, retrieval
- `Infrastructure/Persistence`: SQLite schema, repository, vector store
- `Infrastructure/Wikipedia`: Wikipedia content boundary

The WASM client is now split into:

- `Components/ChatSidebar`
- `Components/ChatThread`
- `Components/MessageView`
- `Components/CitationList`
- `Components/GraphView`
- `Services/ApiClient`

## Prerequisites

- .NET 10 SDK
- SQLite is bundled through `Microsoft.Data.Sqlite`; no separate database server is required

## Configuration

No external API keys are required for the current implementation.

The app does not call OpenAI, Semantic Kernel, or external Wikipedia services at runtime yet. The current backend generates grounded study-guide text, citations, and topic graphs from the prompt and persists them locally in SQLite.

Optional configuration:

- `WikiGraph.Api/appsettings.json`
- `WikiGraph.Api/appsettings.Development.json`

The API uses the `ConnectionStrings:WikiGraph` value if provided. If omitted, it falls back to:

```json
Data Source=wikigraph.db
```

## Run the API

From the repo root:

```bash
dotnet run --project WikiGraph.Api/WikiGraph.Api.csproj
```

The API exposes:

- `GET /api/health`
- `GET /api/sessions`
- `POST /api/sessions`
- `GET /api/sessions/{sessionId}`
- `GET /api/sessions/{sessionId}/graphs`
- `POST /api/query`

## Run the Blazor WebAssembly UI

The WASM client lives in `WikiGraph.Client`.

```bash
dotnet run --project WikiGraph.Client/WikiGraph.Client.csproj
```

The client expects the API to be reachable at:

```text
http://localhost:5000
```

If you want to use a different API host or port, update:

- `WikiGraph.Client/wwwroot/appsettings.json`

## Run the server-rendered web shell

The repository also includes `WikiGraph.Web`, which renders the same study-guide layout on the server side.

```bash
dotnet run --project WikiGraph.Web/WikiGraph.Web.csproj
```

## Test the API and persistence

Build and run the test project:

```bash
dotnet test WikiGraph.Tests/WikiGraph.Tests.csproj
```

The tests cover:

- SQLite-backed session persistence
- API query endpoint behavior

## Generate UML artifacts

The implementation diagrams in `README.md` are extracted automatically when the API project builds.

Manual generation:

```bash
bash scripts/generate-uml.sh README.md docs/uml
```

Generated files:

- `docs/uml/*.puml`
- `docs/uml/README.md`

If `plantuml` is installed, or `PLANTUML_JAR` points to a PlantUML jar, SVG files are rendered automatically too.

### Test runner note

In this container on March 29, 2026:

- `dotnet build WikiGraph.Api/WikiGraph.Api.csproj --no-restore` succeeds and generates UML artifacts in `docs/uml`.
- `dotnet build WikiGraph.Web/WikiGraph.Web.csproj` succeeds.
- `dotnet build WikiGraph.Client/WikiGraph.Client.csproj --no-restore` is blocked by a WebAssembly SDK task-host error:

```text
Could not run the "ComputeWasmBuildAssets" task because MSBuild could not create or connect to a task host with runtime "NET" and architecture "x64".
```

- `dotnet build WikiGraph.Tests/WikiGraph.Tests.csproj` currently aborts early in this container with an opaque MSBuild failure that does not surface a normal diagnostic message.
- `dotnet test` currently aborts with an ARM64 VSTest host lookup error:

```text
Could not find 'dotnet' host for the 'ARM64' architecture.
```

If you hit this locally, confirm that your installed .NET SDK/runtime set matches the repo target framework and that the WebAssembly and test host workloads for your platform are available.

## Validate endpoints manually

You can use the included HTTP file:

- `WikiGraph.Api/WikiGraph.Api.http`

Or call the API directly:

```bash
curl http://localhost:5000/api/health
curl http://localhost:5000/api/sessions
```

Example query:

```bash
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"demo","prompt":"Climate adaptation","sourceUrl":null}'
```

## Data location

By default, SQLite creates:

- `wikigraph.db` in the API working directory

Delete that file if you want to reset local sessions and graphs.

## Notes on current behavior

- The browser UI shows sessions, thread history, citations, and graph data.
- The API persists all session content to SQLite.
- The current implementation is local-first and does not require cloud credentials.
