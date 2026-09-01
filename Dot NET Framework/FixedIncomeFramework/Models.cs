using System;

namespace FixedIncomeFramework.Models
{
    public class Bond
    {
        public string Isin { get; set; }
        public string Issuer { get; set; }
        public string Currency { get; set; }
        public decimal FaceValue { get; set; }
        public decimal CouponRate { get; set; }
        public DateTime MaturityDate { get; set; }
        public string Status { get; set; }

        public Bond(string isin, string issuer, string currency, decimal faceValue, decimal couponRate, DateTime maturityDate, string status)
        {
            Isin = isin; Issuer = issuer; Currency = currency;
            FaceValue = faceValue; CouponRate = couponRate;
            MaturityDate = maturityDate; Status = status;
        }
    }

    public class TreasuryBill
    {
        public string BillId { get; set; }
        public decimal FaceValue { get; set; }
        public decimal DiscountRate { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime MaturityDate { get; set; }
        public string Status { get; set; }

        public TreasuryBill(string billId, decimal faceValue, decimal discountRate, DateTime issueDate, DateTime maturityDate, string status)
        {
            BillId = billId; FaceValue = faceValue; DiscountRate = discountRate;
            IssueDate = issueDate; MaturityDate = maturityDate; Status = status;
        }
    }

    public class YieldCurvePoint
    {
        public string Tenor { get; set; }
        public decimal Yield { get; set; }
        public DateTime AsAt { get; set; }

        public YieldCurvePoint(string tenor, decimal yield, DateTime asAt)
        {
            Tenor = tenor; Yield = yield; AsAt = asAt;
        }
    }

    public class PortfolioSummary
    {
        public int TotalBonds { get; set; }
        public int TotalTreasuryBills { get; set; }
        public decimal TotalFaceValue { get; set; }
        public decimal WeightedAvgCoupon { get; set; }
        public string Currency { get; set; }
        public DateTime AsAt { get; set; }

        public PortfolioSummary(int totalBonds, int totalTreasuryBills, decimal totalFaceValue, decimal weightedAvgCoupon, string currency, DateTime asAt)
        {
            TotalBonds = totalBonds; TotalTreasuryBills = totalTreasuryBills;
            TotalFaceValue = totalFaceValue; WeightedAvgCoupon = weightedAvgCoupon;
            Currency = currency; AsAt = asAt;
        }
    }

    public class Settlement
    {
        public string SettlementId { get; set; }
        public string Isin { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public Settlement(string settlementId, string isin, string type, decimal amount, string currency, string status, DateTime createdAt)
        {
            SettlementId = settlementId; Isin = isin; Type = type;
            Amount = amount; Currency = currency; Status = status; CreatedAt = createdAt;
        }
    }

    public class SettlementRequest
    {
        public string Isin { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
}
