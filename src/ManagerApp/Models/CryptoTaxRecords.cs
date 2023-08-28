using FileHelpers;

namespace ManagerApp.Models
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]
    public sealed class CryptoTaxRecords
    {
          
        [FieldOrder(1)]
        public string Name { get; set; }
        [FieldOrder(2)]
        public double SellAmount { get; set; }

        [FieldOrder(3)]
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime BoughtDate { get; set; }
        [FieldOrder(4)]
        public double BuyPrice { get; set; }
        [FieldOrder(5)]
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime SellDate { get; set; }
        [FieldOrder(6)]
        public double SellPrice { get; set; }
        [FieldOrder(7)]
        public string Currency { get; set; }
        [FieldOrder(8)]
        public double CapitalGainAmount { get; set; }
        [FieldOrder(9)]
        public string Calculation { get; set; }
    }
}
