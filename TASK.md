# WikiGraph Task Notes

## Summary

This repository implements a Wikipedia research workspace with:

- A Blazor WebAssembly client
- An ASP.NET Core REST API
- SQLite-backed persistence for sessions, messages, citations, and graphs
- Shared DTO contracts
- Tests for persistence and API behavior

## What was implemented

- Controller-based API endpoints for sessions, health, and query submission
- Layered application structure with orchestrator, summarizer, ingestion, retrieval, graph, repository, and vector-store services
- SQLite persistence for sessions, messages, citations, graphs, pages, chunks, and embeddings
- Browser UI components for sessions, conversation history, citations, and graphs via a typed `ApiClient`
- Shared contracts for API/UI communication
- Automatic PlantUML extraction into `docs/uml` from the implementation Markdown
- Test coverage for persistence and API responses

## How to run

### API

```bash
dotnet run --project WikiGraph.Api/WikiGraph.Api.csproj
```

### Blazor WebAssembly UI

```bash
dotnet run --project WikiGraph.Client/WikiGraph.Client.csproj
```

### Server-rendered UI shell

```bash
dotnet run --project WikiGraph.Web/WikiGraph.Web.csproj
```

### Tests

```bash
dotnet test WikiGraph.Tests/WikiGraph.Tests.csproj
```

## Configuration

No external API keys are required for the current code.

The API uses SQLite and defaults to:

```text
Data Source=wikigraph.db
```

You can override the connection string in:

- `WikiGraph.Api/appsettings.json`
- `WikiGraph.Api/appsettings.Development.json`

## Validation status

- `dotnet build WikiGraph.Api/WikiGraph.Api.csproj --no-restore` succeeds and generates UML artifacts in `docs/uml`.
- `dotnet build WikiGraph.Web/WikiGraph.Web.csproj` succeeds.
- `dotnet build WikiGraph.Client/WikiGraph.Client.csproj --no-restore` is currently blocked in this container by:

```text
Could not run the "ComputeWasmBuildAssets" task because MSBuild could not create or connect to a task host with runtime "NET" and architecture "x64".
```

- `dotnet build WikiGraph.Tests/WikiGraph.Tests.csproj` currently aborts early in this container with an opaque MSBuild failure.
- `dotnet test` is still blocked in this container by an ARM64 VSTest host lookup error:

```text
Could not find 'dotnet' host for the 'ARM64' architecture.
```

## Notes

- The Blazor WebAssembly client uses the API at `http://localhost:5000` by default.
- SQLite persists sessions and their related records locally.
- The browser UI shows reloadable sessions, thread history, citations, and graph data.
