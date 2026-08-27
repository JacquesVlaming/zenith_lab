using Microsoft.AspNetCore.Mvc;
using FixedIncomeTest.Models;

namespace FixedIncomeTest.Controllers;

[ApiController]
[Route("api/bonds")]
public class BondsController : ControllerBase
{
    private static readonly List<Bond> _bonds = new()
    {
        new Bond("NGN0001234567", "Federal Government of Nigeria", "NGN", 50_000_000, 14.25m, new DateTime(2027, 3, 15), "Active"),
        new Bond("NGN0002345678", "Lagos State Government",        "NGN", 25_000_000, 13.50m, new DateTime(2026, 9, 30), "Active"),
        new Bond("NGN0003456789", "Access Bank PLC",               "NGN", 10_000_000, 15.00m, new DateTime(2028, 6, 1),  "Active"),
        new Bond("USD0001234567", "Zenith Bank USD Bond",          "USD",  5_000_000, 7.50m,  new DateTime(2029, 1, 15), "Active"),
        new Bond("NGN0004567890", "Dangote Industries",            "NGN", 30_000_000, 14.75m, new DateTime(2025, 12, 31), "Matured"),
    };

    [HttpGet]
    public IActionResult GetAll() => Ok(_bonds);

    [HttpGet("{isin}")]
    public IActionResult GetByIsin(string isin)
    {
        var bond = _bonds.FirstOrDefault(b => b.Isin.Equals(isin, StringComparison.OrdinalIgnoreCase));
        if (bond is null) return NotFound(new { message = $"Bond with ISIN {isin} not found." });
        return Ok(bond);
    }
}
