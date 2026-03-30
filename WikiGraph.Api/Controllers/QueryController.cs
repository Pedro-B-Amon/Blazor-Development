using Microsoft.AspNetCore.Mvc;
using WikiGraph.Api.Application.Abstractions;
using WikiGraph.Contracts;

namespace WikiGraph.Api.Controllers;

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
    public ActionResult<QueryResponse> Query([FromBody] QueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "SessionId and Prompt are required." });
        }

        return Ok(_queryOrchestrator.Execute(request));
    }
}
