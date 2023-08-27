namespace ManagerApp.Models
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
}
