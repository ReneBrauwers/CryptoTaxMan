namespace TaxCalculator.Models
{
    public sealed class CryptoWalletCollection
    {
        public DateOnly CreatedOn { get; set; }
        public string Name { get; set; }
        public double Available { get; set; }
        public double Value { get; set; }
        public string Currency { get; set; }

        public List<CryptoTaxRecords> RecordedTransactions { get; set; }


    }

    public sealed class CryptoWalletInfo
    {
        public DateTime CreatedOn { get; set; }
       
        public string Token { get; set; }
        public double Available { get; set; }
        public double BuyPrice { get; set; }
        public string Currency { get; set; }
    }
}
