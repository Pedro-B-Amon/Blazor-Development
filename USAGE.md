# WikiGraph Usage Guide

## What Runs Where

- `WikiGraph.Api`: ASP.NET Core API that owns sessions, Wikipedia lookup, Gemini replies, and SQLite persistence.
- `WikiGraph.Client`: Blazor WebAssembly UI that shows sessions, chat messages, citations, and graphs.
- `WikiGraph.Contracts`: Shared DTOs used by both projects.
- `WikiGraph.Tests`: API and persistence tests.

The app is session-based. Each session stores the conversation history, the assistant reply, citations, and the current graph data.

## Prerequisites

- .NET 10 SDK
- No external database server is required
- An optional Gemini API key if you want AI-generated replies and graph topics

## Configuration

The API checks for configuration in this order:

1. `appsettings.json`
2. environment variables
3. a local `.env` file found while starting the API

Recommended setup for local development:

```bash
GEMINI_API_KEY="your-key-here"
Gemini__TextModel="gemini-2.5-flash"
Gemini__EmbeddingModel="gemini-embedding-001"
```

You can place that line in a `.env` file at the repo root or set the variable in your shell before launching the API.

If the key is missing, the app still works, but it uses local fallback text instead of Gemini.

## Run the API

From the repo root:

```bash
dotnet run --project WikiGraph/WikiGraph.Api/WikiGraph.Api.csproj
```

The API is available on the default ASP.NET Core development ports. The client expects the API at `http://localhost:5052` unless you change the client configuration.

## Run the Client

In a separate terminal:

```bash
dotnet run --project WikiGraph/WikiGraph.Client/WikiGraph.Client.csproj
```

The client lets you:

- create and select sessions
- submit a Wikipedia topic or URL
- read the assistant answer
- view citations
- view topic graphs

## API Endpoints

The current endpoints are:

- `GET /api/health`
- `GET /api/sessions`
- `POST /api/sessions`
- `GET /api/sessions/{sessionId}`
- `GET /api/sessions/{sessionId}/graphs`
- `POST /api/sessions/{sessionId}/articles`

The `articles` endpoint is the main entry point for the UI. It stores the user input, resolves the Wikipedia page, asks Gemini for the answer, and returns the updated session.

## Testing

Run the full solution tests with:

```bash
dotnet test WikiGraph/WikiGraph.slnx -m:1 -p:UseSharedCompilation=false
```

The tests cover:

- session creation
- article submission
- graph retrieval
- SQLite persistence

## Local Data

SQLite stores data in `wikigraph.db` in the API working directory.

Delete that file if you want to reset the app.

## Notes

- Restart the API after changing `.env`, because the file is loaded at startup.
- The UI can load graphs either through the full session payload or through the dedicated graphs endpoint.
- With `GEMINI_API_KEY` configured, Semantic Kernel uses Gemini for both chat replies and embeddings while SQLite still stores the sessions, citations, and retrieved chunks.
