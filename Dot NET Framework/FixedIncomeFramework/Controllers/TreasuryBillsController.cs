using System;
using System.Collections.Generic;
using System.Web.Http;
using FixedIncomeFramework.Models;

namespace FixedIncomeFramework.Controllers
{
    [RoutePrefix("api/treasurybills")]
    public class TreasuryBillsController : ApiController
    {
        private static readonly List<TreasuryBill> _bills = new List<TreasuryBill>
        {
            new TreasuryBill("TB-091-2026-001", 100000000m, 18.50m, new DateTime(2026, 5, 28), new DateTime(2026, 8, 25), "Active"),
            new TreasuryBill("TB-182-2026-001", 200000000m, 19.25m, new DateTime(2026, 2, 26), new DateTime(2026, 8, 27), "Active"),
            new TreasuryBill("TB-364-2026-001", 500000000m, 20.10m, new DateTime(2025, 8, 29), new DateTime(2026, 8, 27), "Active"),
            new TreasuryBill("TB-091-2026-002", 150000000m, 18.75m, new DateTime(2026, 6, 4),  new DateTime(2026, 9, 1),  "Active"),
        };

        [HttpGet, Route("")]
        public IHttpActionResult GetAll() => Ok(_bills);
    }
}
