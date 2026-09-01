using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using FixedIncomeFramework.Models;

namespace FixedIncomeFramework.Controllers
{
    [RoutePrefix("api/bonds")]
    public class BondsController : ApiController
    {
        private static readonly List<Bond> _bonds = new List<Bond>
        {
            new Bond("NGN0001234567", "Federal Government of Nigeria", "NGN", 50000000m,  14.25m, new DateTime(2027, 3, 15),  "Active"),
            new Bond("NGN0002345678", "Lagos State Government",        "NGN", 25000000m,  13.50m, new DateTime(2026, 9, 30),  "Active"),
            new Bond("NGN0003456789", "Access Bank PLC",               "NGN", 10000000m,  15.00m, new DateTime(2028, 6, 1),   "Active"),
            new Bond("USD0001234567", "Zenith Bank USD Bond",          "USD",  5000000m,   7.50m, new DateTime(2029, 1, 15),  "Active"),
            new Bond("NGN0004567890", "Dangote Industries",            "NGN", 30000000m,  14.75m, new DateTime(2025, 12, 31), "Matured"),
        };

        [HttpGet, Route("")]
        public IHttpActionResult GetAll() => Ok(_bonds);

        [HttpGet, Route("{isin}")]
        public IHttpActionResult GetByIsin(string isin)
        {
            var bond = _bonds.FirstOrDefault(b => b.Isin.Equals(isin, StringComparison.OrdinalIgnoreCase));
            if (bond == null) return NotFound();
            return Ok(bond);
        }
    }
}
