using Microsoft.AspNetCore.Mvc;
using WikiGraph.Api.Infrastructure.Persistence;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Controllers;

/// <summary>
/// Exposes the session list and session detail endpoints used by the browser UI.
/// </summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;

    public SessionController(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    [HttpGet]
    public ActionResult<IReadOnlyList<SessionSummary>> GetSessions() => Ok(_sessionRepository.GetSessions());

    [HttpPost]
    public ActionResult<SessionSummary> CreateSession([FromBody] CreateSessionRequest? input)
    {
        // Keep the client simple by filling in the default title server-side when the request omits one.
        var title = input?.Title is { Length: > 0 } ? input.Title : "New session";
        var session = _sessionRepository.CreateSession(title);
        return Created($"/api/sessions/{session.SessionId}", session);
    }

    [HttpGet("{sessionId}")]
    public ActionResult<SessionDetailDto> GetSession(string sessionId)
    {
        return _sessionRepository.GetSession(sessionId) is { } session ? Ok(session) : NotFound();
    }

    [HttpGet("{sessionId}/graphs")]
    public ActionResult<IReadOnlyList<GraphDto>> GetGraphs(string sessionId)
    {
        return _sessionRepository.GetGraphs(sessionId) is { } graphs ? Ok(graphs) : NotFound();
    }
}
