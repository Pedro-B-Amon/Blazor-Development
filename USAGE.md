# WikiGraph Usage Guide

## What this project contains

- `WikiGraph.Api`: ASP.NET Core REST API with SQLite persistence.
- `WikiGraph.Client`: Blazor WebAssembly UI.
- `WikiGraph.Web`: Additional Blazor UI shell in the repo, kept buildable alongside the WASM client.
- `WikiGraph.Contracts`: Shared DTOs used by the API, UI, and tests.
- `WikiGraph.Tests`: API and persistence tests.

## Prerequisites

- .NET 9 SDK
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

If you want to use a different API host or port, update the hardcoded API base URL in:

- `WikiGraph.Client/Pages/Home.razor`

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

### Test runner note

In this container, `dotnet test` currently aborts with an ARM64 VSTest host lookup error:

```text
Could not find 'dotnet' host for the 'ARM64' architecture.
```

The projects themselves build successfully. If you hit this locally, confirm that the ARM64 .NET host/runtime for your platform is installed and that `dotnet --info` reports a matching runtime environment.

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
