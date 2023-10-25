using FileHelpers;
namespace TaxCalculator.Models
{

    [DelimitedRecord(",")]
    public class CryptoTransactionRecord
    {
        public int Sequence { get; set; }
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public DateTime TransactionDate { get; set; }
        public string? TransactionType { get; set; }
        public bool TaxableEvent { get; set; }
        public double? Amount { get; set; }
        public string? AmountAssetType { get; set; }
        public double? ExchangeRateValue { get; set; } = 0d;
        public string? ExchangeRateCurrency { get; set; }
        public double? Value { get; set; } = 0d;
        public string? ValueAssetType { get; set; }
        public bool IsNFT { get; set; } = false;
        public bool UsesManualAssignedExchangeRate { get; set; } = false;
        public bool FlaggedForReview { get; set; } = false;
        public string? InternalNotes { get; set; }
        //public string? FeeCurrency;
        //public double? ExchangeRate;
        //public string? ExchangeCurrency;
        //[FieldQuoted]
        //public string? Notes;
    }
}
