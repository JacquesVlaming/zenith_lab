using System;
using System.Collections.Generic;
using System.Web.Http;
using FixedIncomeFramework.Models;

namespace FixedIncomeFramework.Controllers
{
    [RoutePrefix("api/yield-curve")]
    public class YieldCurveController : ApiController
    {
        [HttpGet, Route("")]
        public IHttpActionResult Get()
        {
            var asAt = DateTime.UtcNow;
            var curve = new List<YieldCurvePoint>
            {
                new YieldCurvePoint("91-Day",  18.50m, asAt),
                new YieldCurvePoint("182-Day", 19.25m, asAt),
                new YieldCurvePoint("364-Day", 20.10m, asAt),
                new YieldCurvePoint("2-Year",  20.75m, asAt),
                new YieldCurvePoint("3-Year",  21.00m, asAt),
                new YieldCurvePoint("5-Year",  21.50m, asAt),
                new YieldCurvePoint("7-Year",  21.75m, asAt),
                new YieldCurvePoint("10-Year", 22.00m, asAt),
            };
            return Ok(curve);
        }
    }
}
