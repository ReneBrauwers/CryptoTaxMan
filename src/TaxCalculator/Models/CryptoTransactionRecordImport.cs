using FileHelpers;

namespace TaxCalculator.Models
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]
    public class CryptoTransactionRecordImport
    {
        public int Sequence = 0;
        [FieldConverter(ConverterKind.Date, "yyyy-MM-ddTHH:mm:ss")]
        public DateTime TransactionDate = DateTime.Today;
        [FieldCaption("TransactionType")]
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? AssetType;
        public double? AmountIn;
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? CurrencyIn;
        public double? AmountOut;
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? CurrencyOut;
        [FieldCaption("TransactionEvent")]
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? TransactionType;
        public double? Fee;
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? FeeCurrency;
        //public double? ExchangeRate;
        //public string? ExchangeCurrency;
        [FieldQuoted]
        [FieldConverter(typeof(LowercaseStringConverter))]
        public string? Notes;

    
    }


    public class LowercaseStringConverter : ConverterBase
    {
        public override object StringToField(string from)
        {
            return from.ToLower();
        }

        public override string FieldToString(object fieldValue)
        {
            return fieldValue.ToString();
        }
    }

}
