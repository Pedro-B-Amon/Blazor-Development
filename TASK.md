# WikiGraph Task Notes

## Summary

This repository implements a Wikipedia research workspace with:

- A Blazor WebAssembly client
- An ASP.NET Core REST API
- SQLite-backed persistence for sessions, messages, citations, and graphs
- Shared DTO contracts
- Tests for persistence and API behavior

## What was implemented

- Session management endpoints
- Query submission endpoint
- SQLite persistence for all session artifacts
- Browser UI for sessions, conversation history, citations, and topic graphs
- Shared contracts for API/UI communication
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

- `dotnet build` succeeds for the API, WASM client, web shell, contracts, and tests.
- `dotnet test` is still blocked in this container by an ARM64 VSTest host lookup error:

```text
Could not find 'dotnet' host for the 'ARM64' architecture.
```

## Notes

- The Blazor WebAssembly client uses the API at `http://localhost:5000` by default.
- SQLite persists sessions and their related records locally.
- The browser UI shows reloadable sessions, thread history, citations, and graph data.
