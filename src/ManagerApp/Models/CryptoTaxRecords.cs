using FileHelpers;

namespace ManagerApp.Models
{
    [IgnoreFirst(1)]
    [DelimitedRecord(",")]
    public sealed class CryptoTaxRecords
    {
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime BoughtDate { get; set; }
        [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] //THH:mm:ss")]
        public DateTime SellDate { get; set; }
        public string Name { get; set; }
        public double SellAmount { get; set; }
        public double BuyPrice { get; set; }
        public double SellPrice { get; set; }
        public string Currency { get; set; }
        public double CapitalGainAmount { get; set; }
        public string Calculation { get; set; }
    }
}
