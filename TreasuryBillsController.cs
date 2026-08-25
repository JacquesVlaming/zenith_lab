using Microsoft.AspNetCore.Mvc;
using FixedIncomeTest.Models;

namespace FixedIncomeTest.Controllers;

[ApiController]
[Route("api/treasurybills")]
public class TreasuryBillsController : ControllerBase
{
    private static readonly List<TreasuryBill> _bills = new()
    {
        new TreasuryBill("TB-091-2026-001", 100_000_000, 18.50m, new DateOnly(2026, 5, 28), new DateOnly(2026, 8, 25), "Active"),
        new TreasuryBill("TB-182-2026-001", 200_000_000, 19.25m, new DateOnly(2026, 2, 26), new DateOnly(2026, 8, 27), "Active"),
        new TreasuryBill("TB-364-2026-001", 500_000_000, 20.10m, new DateOnly(2025, 8, 29), new DateOnly(2026, 8, 27), "Active"),
        new TreasuryBill("TB-091-2026-002", 150_000_000, 18.75m, new DateOnly(2026, 6, 4),  new DateOnly(2026, 9, 1),  "Active"),
    };

    [HttpGet]
    public IActionResult GetAll() => Ok(_bills);
}
