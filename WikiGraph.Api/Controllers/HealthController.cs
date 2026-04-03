using Microsoft.AspNetCore.Mvc;

namespace WikiGraph.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    // Returns a minimal health payload for probes and smoke tests.
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
