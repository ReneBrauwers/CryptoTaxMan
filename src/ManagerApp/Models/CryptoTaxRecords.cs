namespace ManagerApp.Models
{
    public sealed class CryptoTaxRecords
    {
        public DateOnly SellDate { get; set; }
        public string Name { get; set; }
        public double SellAmount { get; set; }
        public double BuyPrice { get; set; }
        public double SellPrice { get; set; }
        public string Currency { get; set; }
        public double CapitalGainAmount { get; set; }
        public string Calculation { get; set; }
    }
}
