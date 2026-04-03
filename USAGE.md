# Usage

WikiGraph has two entry points:

- Backend API: `WikiGraph.Api`
- Frontend app: `WikiGraph.Web`

## Run The Backend And Frontend

Use two terminals:

```bash
dotnet run --project WikiGraph.Api/WikiGraph.Api.csproj
```

```bash
dotnet run --project WikiGraph.Web/WikiGraph.Web.csproj
```

Or run both from one shell on macOS/Linux:

```bash
dotnet run --project WikiGraph.Api/WikiGraph.Api.csproj & dotnet run --project WikiGraph.Web/WikiGraph.Web.csproj
```

## Ports

- API: `http://localhost:5052`
- Frontend: `http://localhost:5039`

## Notes

- The backend stores data in SQLite.
- The frontend talks to the API while you browse the site.
- If you change local environment values, restart the backend.

