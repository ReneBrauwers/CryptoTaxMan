using Common.Enums;
using System.Text.Json.Serialization;

namespace Common.Models
{
    public class CryptoUserTransaction
    {
        public int Sequence { get; set; }
        public DateTime? TransactionDate { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransactionAssetType TransactionType { get; set; }
        public decimal? AmountIn { get; set; }
        public string? CurrencyIn { get; set; }      
        public decimal? AmountOut { get; set; }
        public string? CurrencyOut { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TransactionEventType TransactionEvent { get; set; }
        public decimal? Fee { get; set; }
        public string? FeeCurrency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? ExchangeCurrency { get; set; }
        public string? Notes { get; set; }
       
    }
}