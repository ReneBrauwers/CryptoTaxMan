namespace TaxCalculator.Models
{
    public class CryptoResult
    {
        public List<CryptoTaxRecords> TaxRecords { get; set; }
        public List<CryptoCollection> UnsoldCrypto { get; set; }
        public List<CryptoCollection> AggregatedUnsoldCrypto { get; set; }
    }
}
