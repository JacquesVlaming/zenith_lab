using Microsoft.AspNetCore.Mvc;
using FixedIncomeTest.Models;

namespace FixedIncomeTest.Controllers;

[ApiController]
[Route("api/settlements")]
public class SettlementsController : ControllerBase
{
    private static readonly List<Settlement> _settlements = new()
    {
        new Settlement("STL-20260824-001", "NGN0001234567", "Coupon",    7_125_000, "NGN", "Settled",  DateTime.UtcNow.AddDays(-1)),
        new Settlement("STL-20260824-002", "TB-364-2026-001","Maturity", 500_000_000,"NGN","Pending",  DateTime.UtcNow.AddHours(-3)),
        new Settlement("STL-20260823-001", "NGN0002345678", "Coupon",    1_687_500, "NGN", "Settled",  DateTime.UtcNow.AddDays(-2)),
    };

    [HttpGet]
    public IActionResult GetAll() => Ok(_settlements);

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var settlement = _settlements.FirstOrDefault(s => s.SettlementId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (settlement is null) return NotFound(new { message = $"Settlement {id} not found." });
        return Ok(settlement);
    }

    [HttpPost]
    public IActionResult Create([FromBody] SettlementRequest request)
    {
        var id = $"STL-{DateTime.UtcNow:yyyyMMdd}-{_settlements.Count + 1:D3}";
        var settlement = new Settlement(
            id,
            request.Isin,
            request.Type,
            request.Amount,
            request.Currency,
            "Pending",
            DateTime.UtcNow
        );
        _settlements.Add(settlement);
        return CreatedAtAction(nameof(GetById), new { id = settlement.SettlementId }, settlement);
    }
}
