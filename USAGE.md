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
- Optional: an OpenAI API key if you want Semantic Kernel chat completion and semantic embeddings

## Configuration

The app works in two modes:

- With OpenAI configured: Semantic Kernel is used for chat completion and embeddings.
- Without OpenAI configured: the backend falls back to deterministic local summarization and keyword-based retrieval.

Optional configuration:

- `WikiGraph.Api/appsettings.json`
- `WikiGraph.Api/appsettings.Development.json`

The API uses the `ConnectionStrings:WikiGraph` value if provided. If omitted, it falls back to:

```json
Data Source=wikigraph.db
```

OpenAI configuration is read from the `OpenAI` section in `WikiGraph.Api/appsettings.json`, with environment-variable fallbacks for secrets:

```bash
export OPENAI_API_KEY=your-key
export OPENAI_ORG_ID=your-org-id   # optional
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

`POST /api/query` accepts either a topic prompt, a Wikipedia URL, or both.

## Run the Blazor WebAssembly UI

The WASM client lives in `WikiGraph.Client`.

```bash
dotnet run --project WikiGraph.Client/WikiGraph.Client.csproj
```

The client expects the API to be reachable at:

```text
http://localhost:5052
```

If you want to use a different API host or port, update:

- `WikiGraph.Client/wwwroot/appsettings.json`
- `WikiGraph.Client/Program.cs`

The chat form supports:

- topic prompts
- Wikipedia URLs
- combined prompt + URL submissions

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

## Validate endpoints manually

You can use the included HTTP file:

- `WikiGraph.Api/WikiGraph.Api.http`

Or call the API directly:

```bash
curl http://localhost:5052/api/health
curl http://localhost:5052/api/sessions
```

Example query:

```bash
curl -X POST http://localhost:5052/api/query \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"demo","prompt":"Climate adaptation","sourceUrl":null}'
```

Example URL-driven query:

```bash
curl -X POST http://localhost:5052/api/query \
  -H "Content-Type: application/json" \
  -d '{"sessionId":"demo","prompt":"","sourceUrl":"https://en.wikipedia.org/wiki/Climate_change_adaptation"}'
```

## Data location

By default, SQLite creates:

- `wikigraph.db` in the API working directory

Delete that file if you want to reset local sessions and graphs.

## Notes on current behavior

- The browser UI shows sessions, thread history, citations, and one or more topic graphs.
- The API persists all session content to SQLite.
- Citations include chunk identifiers so responses stay linked to retrieved context.
- OpenAI credentials are optional; without them the app still runs using deterministic local fallbacks.
