using FileHelpers;
using Shared.Enums;
using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class InvalidCryptoUserTransaction
    {
        public string AssetType { get; set; }
        public decimal AvailableBalance { get; set; }
        public decimal TransactedBalance { get; set; }

        public List<TransactionReferenceTrail> TransactionsReferenceTrails { get; set; }

    }

    public class TransactionReferenceTrail
    {
        public int Sequence { get; set; }
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")]
        public DateTime? TransactionDate { get; set; }   
        public TransactionEventType TransactionType { get; set; }
        public decimal? Amount { get; set; }
        public string? AssetType { get; set; }
        public decimal? BalanceAfter { get; set; }
   
    }
}

 