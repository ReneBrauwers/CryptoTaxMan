using FileHelpers;
namespace ManagerApp.Models
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]   
    public class CryptoTransactionRecordImport
    {
        public int Sequence = 0;
        [FieldConverter(ConverterKind.Date, "yyyy-MM-ddTHH:mm:ss")]
        public DateTime TransactionDate = DateTime.Today;
        [FieldCaption("TransactionType")]
        public string? AssetType;        
        public double? AmountIn;
        public string? CurrencyIn;
        public double? AmountOut;
        public string? CurrencyOut;
        [FieldCaption("TransactionEvent")]
        public string? TransactionType;
        public double? Fee;
        public string? FeeCurrency;
        //public double? ExchangeRate;
        //public string? ExchangeCurrency;
        [FieldQuoted]
        public string? Notes;
    }
}
