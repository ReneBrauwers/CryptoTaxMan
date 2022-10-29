using Common.Models;
using ManagerApp.Models;

namespace ManagerApp.Services
{
    public static class CryptoTaxFactory
    {
        public static List<CryptoTaxRecords> CreateCryptoTaxRecords(List<CryptoCollection> cryptoCollection, List<CryptoTransactionRecord> records, List<ExchangeRate> exchangeRates)
        {
            //we will be leveraging High-in First-out (HIFO)
            var result = new List<CryptoTaxRecords>();
            foreach (var taxableTransactions in records.Where(x => x.TaxableEvent).OrderBy(x => x.TransactionDate))
            {
                //retrieve sell exchange rate
                var sellExchangeRate = Convert.ToDouble(exchangeRates.FirstOrDefault(x => x.Date == taxableTransactions.TransactionDate && x.Symbol?.ToLower() == taxableTransactions?.AmountAssetType?.ToLower())?.Low);

                //lookup HIFO entry
                var shortlistedCollections = cryptoCollection.Where(x => x.CreatedOn <= DateOnly.FromDateTime(taxableTransactions.TransactionDate) && x.Name.ToLower() == taxableTransactions?.AmountAssetType?.ToLower() && x.Available > 0);
                var sellAmount = taxableTransactions.Amount;
                foreach(var matchedCollections in shortlistedCollections.OrderByDescending(x=>x.BoughtAt))
                {
                    //check if current item has sufficient funds
                    if((matchedCollections.Available - sellAmount) >=0)
                    {
                        //sufficient
                        result.Add(new CryptoTaxRecords()
                        {
                            BuyPrice = matchedCollections.BoughtAt,
                            Currency = matchedCollections.Currency,
                            Name = matchedCollections.Name,
                            SellAmount = sellAmount ?? 0d,
                            SellDate = DateOnly.FromDateTime(taxableTransactions.TransactionDate),
                            SellPrice = taxableTransactions.ExchangeRateValue ?? 0d,
                            CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * sellAmount ?? 0d,
                            Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {sellAmount ?? 0d}"

                        }) ;

                        //update collection
                       
                        matchedCollections.Available = matchedCollections.Available - sellAmount ?? 0d;
                        sellAmount = 0d;
                        //exit foreach
                        break;
                    }
                    else
                    {
                        //insufficient so max out, current
                        double availableAmount = matchedCollections.Available;
                        
                        result.Add(new CryptoTaxRecords()
                        {
                            BuyPrice = matchedCollections.BoughtAt,
                            Currency = matchedCollections.Currency,
                            Name = matchedCollections.Name,
                            SellAmount = availableAmount,
                            SellDate = DateOnly.FromDateTime(taxableTransactions.TransactionDate),
                            SellPrice = taxableTransactions.ExchangeRateValue ?? 0d,
                            CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * availableAmount,
                            Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {availableAmount}"
                       
                        });
                        //update sell amount remaining
                        sellAmount = sellAmount - availableAmount;

                        //update collection
                        matchedCollections.Available = 0d;
                    }
                }
            
                if(sellAmount > 0)
                {
                    //there is an error; as we are trying to sell whilst there are no funds available.
                    taxableTransactions.InternalNotes = $"Invalid transactions, insufficient funds to sell. Missing {sellAmount} {taxableTransactions?.AmountAssetType?.ToLower()} ";
                }
            }

            return result;
        }

        
    }
}
