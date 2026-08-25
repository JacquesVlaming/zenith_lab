using Microsoft.AspNetCore.Mvc;
using FixedIncomeTest.Models;

namespace FixedIncomeTest.Controllers;

[ApiController]
[Route("api/yield-curve")]
public class YieldCurveController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var asAt = DateTime.UtcNow;
        var curve = new List<YieldCurvePoint>
        {
            new YieldCurvePoint("91-Day",   18.50m, asAt),
            new YieldCurvePoint("182-Day",  19.25m, asAt),
            new YieldCurvePoint("364-Day",  20.10m, asAt),
            new YieldCurvePoint("2-Year",   20.75m, asAt),
            new YieldCurvePoint("3-Year",   21.00m, asAt),
            new YieldCurvePoint("5-Year",   21.50m, asAt),
            new YieldCurvePoint("7-Year",   21.75m, asAt),
            new YieldCurvePoint("10-Year",  22.00m, asAt),
        };
        return Ok(curve);
    }
}
