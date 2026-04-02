using Microsoft.AspNetCore.Mvc;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Controllers;

/// <summary>
/// Validates the minimal query payload and forwards the work to the orchestrator.
/// </summary>
[ApiController]
[Route("api/query")]
public sealed class QueryController : ControllerBase
{
    private readonly IQueryOrchestrator _queryOrchestrator;

    public QueryController(IQueryOrchestrator queryOrchestrator)
    {
        _queryOrchestrator = queryOrchestrator;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResponse>> Query([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        // The backend needs either a prompt or a source URL to derive a canonical article.
        if (string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest(new { error = "SessionId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Prompt) && string.IsNullOrWhiteSpace(request.SourceUrl))
        {
            return BadRequest(new { error = "Prompt or SourceUrl is required." });
        }

        return Ok(await _queryOrchestrator.ExecuteAsync(request, cancellationToken));
    }
}
