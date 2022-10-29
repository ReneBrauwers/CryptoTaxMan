using ManagerApp.Models;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;

namespace ManagerApp.Utils
{
    public static class Taxify
    {
        /// <summary>
        /// Breaks down imported transaction records into buy and sell records
        /// </summary>
        /// <param name="record">Imported CSV tax transaction record</param>
        /// <returns>List of Sell and Buy crypto transactions</returns>
        public static List<CryptoTransactionRecord> Flatten(CryptoTransactionRecordImport record)
        {
            List<CryptoTransactionRecord> taxifiedRecords = new List<CryptoTransactionRecord>();

            switch (record.TransactionType.ToLower())
            {

                case "buy":
                    {
                        taxifiedRecords.Add(new CryptoTransactionRecord
                        {
                            TaxableEvent = false,
                            Amount = record.AmountIn,
                            AmountAssetType = record.CurrencyIn,
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate.Date,
                            TransactionType = record.TransactionType,
                            ExchangeRateCurrency = record.ExchangeCurrency,
                            ExchangeRateValue = record.ExchangeRate,                            
                            IsNFT = false
                        });
                        break;
                    }
                case "nftbuy":
                    {
                        //sell and a buy

                        taxifiedRecords.Add(new CryptoTransactionRecord
                        {
                            TaxableEvent = true,
                            Amount = record.AmountIn,
                            AmountAssetType = record.CurrencyIn,
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate.Date,
                            TransactionType = "sell",
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate,
                            IsNFT = false
                        });


                        taxifiedRecords.Add(new CryptoTransactionRecord
                        {
                            TaxableEvent = false,
                            Amount = record.AmountOut,
                            AmountAssetType = record.CurrencyOut,
                            Sequence = record.Sequence,
                            Value = record.AmountIn,
                            ValueAssetType = record.CurrencyIn,
                            TransactionDate = record.TransactionDate.Date,
                            TransactionType = "buy",
                            IsNFT = true

                        });
                        break;
                    }
                case "stake":
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        bool useProvidedExchangeRate = false;
                        if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        {
                            useProvidedExchangeRate = true;
                        }

                        //exchange rate and exchange currency in a STAKE SELL reflects the exchange rate staked against 
                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate.Date;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = false;                       
                        sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);
                        taxifiedRecords.Add(sellRecord);

                        break;
                    }
                case "transfer":
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        bool useProvidedExchangeRate = false;
                        if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        {
                            useProvidedExchangeRate = true;
                        }

                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = (record.AmountIn - record.AmountOut);
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate.Date;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = false;
                        sellRecord.InternalNotes = "Transfer fees";
                        sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(sellRecord);

                        break;
                    }
                case "sell":
                    {
                        

                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        bool useProvidedExchangeRate = false;
                        if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        {
                            useProvidedExchangeRate = true;
                        }

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoTransactionRecord();
                       

                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate.Date;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = false;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue =  (useProvidedExchangeRate? record.ExchangeRate:0d);

                        taxifiedRecords.Add(sellRecord);

                        var buyRecord = new CryptoTransactionRecord();
                        buyRecord.TaxableEvent = false;
                        buyRecord.Amount = record.AmountOut;
                        buyRecord.AmountAssetType = record.CurrencyOut;
                        buyRecord.Sequence = record.Sequence;
                        buyRecord.TransactionDate = record.TransactionDate.Date;
                        buyRecord.TransactionType = "buy";
                        buyRecord.IsNFT = false;
                        buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(buyRecord);
                        break;
                    }
                case "nftsell":
                    {
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        bool useProvidedExchangeRate = false;
                        if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        {
                            useProvidedExchangeRate = true;
                        }

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.Value = record.AmountOut;
                        sellRecord.ValueAssetType = record.CurrencyOut;
                        sellRecord.TransactionDate = record.TransactionDate.Date;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = true;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(sellRecord);

                        var buyRecord = new CryptoTransactionRecord();
                        buyRecord.TaxableEvent = false;
                        buyRecord.Amount = record.AmountOut;
                        buyRecord.AmountAssetType = record.CurrencyOut;
                        buyRecord.Sequence = record.Sequence;
                        buyRecord.TransactionDate = record.TransactionDate.Date;
                        buyRecord.TransactionType = "buy";
                        buyRecord.IsNFT = false;
                        buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(buyRecord);

                        break;
                    }
                case "unstake":
                    {
                        //buy

                        taxifiedRecords.Add(new CryptoTransactionRecord
                        {
                            TaxableEvent = false,
                            Amount = record.AmountOut,
                            AmountAssetType = record.CurrencyOut,
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate.Date,
                            TransactionType = "buy",
                            IsNFT = false,
                            ExchangeRateCurrency = record.ExchangeCurrency,
                            ExchangeRateValue = record.ExchangeRate
                        });
                        break;
                    }
                default:
                    {
                        break;
                    }
            }


            return taxifiedRecords;
        }

        /// <summary>
        /// Consolidates records into a daily interval
        /// </summary>
        /// <param name="records"></param>
        /// <returns>Consolidated list of transactions</returns>
        public static List<CryptoTransactionRecord> DailyConsolidation(List<CryptoTransactionRecord> records)
        {
            List<CryptoTransactionRecord> result = new List<CryptoTransactionRecord>();
            //group by day
            foreach(var dailyTransactions in records.GroupBy(x => x.TransactionDate))
            {
                //group buys/sells and consolidate
                result.AddRange(ConsolidateTransactions(dailyTransactions.Where(x => x.TransactionType == "buy").ToList()));
                result.AddRange(ConsolidateTransactions(dailyTransactions.Where(x => x.TransactionType == "sell").ToList()));

            }

            //sort by buys 
            result = result.OrderBy(x => x.TransactionType).ThenBy(x => x.TransactionDate).ToList();



            //resequence
            var recordIterator = 0;
            result.ForEach(x =>
            {
                recordIterator++;
                x.Sequence = recordIterator;
            });

            return result;
        }

        private static List<CryptoTransactionRecord> ConsolidateTransactions(List<CryptoTransactionRecord> records)
        {
            List<CryptoTransactionRecord> results = new ();
            foreach (var dailyBuyTransactions in records.GroupBy(x => x.AmountAssetType))
            {
                bool isInitialIteration = true;
                //iterate over buys and aggregate (consolidate)
                CryptoTransactionRecord result = new CryptoTransactionRecord();
                foreach (var transaction in dailyBuyTransactions)
                {
                    if (isInitialIteration)
                    {
                        isInitialIteration = false;

                        result.TransactionDate = transaction.TransactionDate;
                        result.Amount = transaction.Amount;
                        result.AmountAssetType = transaction.AmountAssetType;
                        result.ExchangeRateCurrency = transaction.ExchangeRateCurrency;
                        result.ExchangeRateValue = transaction.ExchangeRateValue;
                        result.InternalNotes = transaction.InternalNotes;
                        result.IsNFT = transaction.IsNFT;
                        result.Sequence = 0;
                        result.TaxableEvent = transaction.TaxableEvent;
                        result.TransactionType = transaction.TransactionType;
                        result.Value = transaction.Value;
                        result.ValueAssetType = transaction.ValueAssetType;
                        result.UsesManualAssignedExchangeRate = transaction.UsesManualAssignedExchangeRate;

                    }
                    else
                    {

                        result.Amount = +transaction.Amount;
                        if (!string.IsNullOrWhiteSpace(transaction.InternalNotes))
                        {
                            result.InternalNotes = transaction.InternalNotes;
                        }

                        result.Sequence = transaction.Sequence;
                        result.Value = +transaction.Value;

                    }
                }
                results.Add(result);
            }

            return results;
        }
      
    }

   
}
