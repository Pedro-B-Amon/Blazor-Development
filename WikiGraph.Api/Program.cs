using WikiGraph.Api;
using WikiGraph.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
builder.Services.AddSingleton<ISessionStore, SqliteSessionStore>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

app.MapGet("/api/sessions", (ISessionStore store) => Results.Ok(store.GetSessions()));

app.MapPost("/api/sessions", (ISessionStore store, CreateSessionRequest? input) =>
{
    var title = input?.Title is { Length: > 0 } ? input.Title : "New session";
    var session = store.CreateSession(title);
    return Results.Created($"/api/sessions/{session.SessionId}", session);
});

app.MapGet("/api/sessions/{sessionId}", (ISessionStore store, string sessionId) =>
    store.GetSession(sessionId) is { } session ? Results.Ok(session) : Results.NotFound());

app.MapGet("/api/sessions/{sessionId}/graphs", (ISessionStore store, string sessionId) =>
    store.GetSession(sessionId) is { } session ? Results.Ok(session.Graphs) : Results.NotFound());

app.MapPost("/api/query", (ISessionStore store, QueryRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "SessionId and Prompt are required." });
    }

    return Results.Ok(store.AppendQuery(request));
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
