using System;
using System.Web.Http;
using FixedIncomeFramework.Models;

namespace FixedIncomeFramework.Controllers
{
    [RoutePrefix("api/portfolio")]
    public class PortfolioController : ApiController
    {
        [HttpGet, Route("summary")]
        public IHttpActionResult GetSummary()
        {
            var summary = new PortfolioSummary(
                totalBonds: 5,
                totalTreasuryBills: 4,
                totalFaceValue: 1070000000m,
                weightedAvgCoupon: 14.60m,
                currency: "NGN",
                asAt: DateTime.UtcNow
            );
            return Ok(summary);
        }
    }
}
