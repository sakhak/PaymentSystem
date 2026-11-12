
using Microsoft.AspNetCore.Mvc;

namespace WorkLog.API.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Application = "WorkLog",
            Timestamp = DateTime.UtcNow
        });
    }
}
