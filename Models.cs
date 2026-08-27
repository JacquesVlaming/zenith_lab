namespace FixedIncomeTest.Models;

public record Bond(
    string Isin,
    string Issuer,
    string Currency,
    decimal FaceValue,
    decimal CouponRate,
    DateTime MaturityDate,
    string Status
);

public record TreasuryBill(
    string BillId,
    decimal FaceValue,
    decimal DiscountRate,
    DateTime IssueDate,
    DateTime MaturityDate,
    string Status
);

public record YieldCurvePoint(
    string Tenor,
    decimal Yield,
    DateTime AsAt
);

public record PortfolioSummary(
    int TotalBonds,
    int TotalTreasuryBills,
    decimal TotalFaceValue,
    decimal WeightedAvgCoupon,
    string Currency,
    DateTime AsAt
);

public record Settlement(
    string SettlementId,
    string Isin,
    string Type,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAt
);

public record SettlementRequest(
    string Isin,
    string Type,
    decimal Amount,
    string Currency
);
