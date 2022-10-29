using ManagerApp.Models;

namespace ManagerApp.Services
{
    public static class CryptoCollectionFactory
    {
        public static List<CryptoCollection> CreateCryptoCollection(List<CryptoTransactionRecord> records)
        {
            var result = new List<CryptoCollection>();
            foreach (var buyRecords in records.Where(x => x.TransactionType == "buy").OrderBy(x => x.TransactionDate))
            {
                result.Add(new CryptoCollection()
                {
                    CreatedOn = DateOnly.FromDateTime(buyRecords.TransactionDate),
                    Available = buyRecords?.Amount ?? 0d,
                    Name = buyRecords?.AmountAssetType ?? Guid.NewGuid().ToString(),
                    BoughtAt = buyRecords?.ExchangeRateValue ?? 0d,
                    Currency = buyRecords?.ExchangeRateCurrency ?? "AUD"
                });
            }

            return result;
        }
    }
}
