using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using FixedIncomeFramework.Models;

namespace FixedIncomeFramework.Controllers
{
    [RoutePrefix("api/settlements")]
    public class SettlementsController : ApiController
    {
        private static readonly List<Settlement> _settlements = new List<Settlement>
        {
            new Settlement("STL-20260824-001", "NGN0001234567",  "Coupon",   7125000m,   "NGN", "Settled", DateTime.UtcNow.AddDays(-1)),
            new Settlement("STL-20260824-002", "TB-364-2026-001","Maturity", 500000000m, "NGN", "Pending", DateTime.UtcNow.AddHours(-3)),
            new Settlement("STL-20260823-001", "NGN0002345678",  "Coupon",   1687500m,   "NGN", "Settled", DateTime.UtcNow.AddDays(-2)),
        };

        [HttpGet, Route("")]
        public IHttpActionResult GetAll() => Ok(_settlements);

        [HttpGet, Route("{id}")]
        public IHttpActionResult GetById(string id)
        {
            var settlement = _settlements.FirstOrDefault(s => s.SettlementId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (settlement == null) return NotFound();
            return Ok(settlement);
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create([FromBody] SettlementRequest request)
        {
            var id = string.Format("STL-{0:yyyyMMdd}-{1:D3}", DateTime.UtcNow, _settlements.Count + 1);
            var settlement = new Settlement(id, request.Isin, request.Type, request.Amount, request.Currency, "Pending", DateTime.UtcNow);
            _settlements.Add(settlement);
            return Created(new Uri(string.Format("api/settlements/{0}", id), UriKind.Relative), settlement);
        }
    }
}
