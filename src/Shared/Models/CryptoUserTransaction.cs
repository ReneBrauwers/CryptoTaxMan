using FileHelpers;
using Shared.Enums;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    [DelimitedRecord(",")]
    public class CryptoUserTransaction
    {
        
        public int Sequence { get; set; }
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        
        public DateTime TransactionDate { get; set; }
       
        public bool TaxableEvent { get; set; }
        public bool ReportableAsIncome { get; set; } = false;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransactionEventType TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string AmountAssetType { get; set; }
        public decimal? ExchangeRateValue { get; set; } = 0m;
        public string? ExchangeRateCurrency { get; set; }
        public decimal? Value { get; set; } = 0m;
        public string? ValueAssetType { get; set; }
        public bool IsNFT { get; set; } = false;
        public bool UsesManualAssignedExchangeRate { get; set; } = false;
        public bool Approved { get; set; } = false;
        public bool ReviewRequired { get; set; } = false;
        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
        public string? InternalNotes { get; set; }

    }
}

 