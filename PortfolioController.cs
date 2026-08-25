using Microsoft.AspNetCore.Mvc;
using FixedIncomeTest.Models;

namespace FixedIncomeTest.Controllers;

[ApiController]
[Route("api/portfolio")]
public class PortfolioController : ControllerBase
{
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        var summary = new PortfolioSummary(
            TotalBonds: 5,
            TotalTreasuryBills: 4,
            TotalFaceValue: 1_070_000_000,
            WeightedAvgCoupon: 14.60m,
            Currency: "NGN",
            AsAt: DateTime.UtcNow
        );
        return Ok(summary);
    }
}
