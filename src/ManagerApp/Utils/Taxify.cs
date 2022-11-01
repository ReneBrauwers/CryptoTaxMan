using Common.Models;
using ManagerApp.Models;
using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

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
                            TransactionDate = record.TransactionDate,
                            TransactionType = record.TransactionType,
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate,
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
                            TransactionDate = record.TransactionDate,
                            TransactionType = "sell",
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate,
                            IsNFT = true
                        });


                        taxifiedRecords.Add(new CryptoTransactionRecord
                        {
                            TaxableEvent = false,
                            Amount = record.AmountOut,
                            AmountAssetType = record.CurrencyOut,
                            Sequence = record.Sequence,
                            Value = record.AmountIn,
                            ValueAssetType = record.CurrencyIn,
                            TransactionDate = record.TransactionDate,
                            TransactionType = "nftbuy",
                            IsNFT = true

                        });
                        break;
                    }
                case "stake":
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a STAKE SELL reflects the exchange rate staked against 
                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = false;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);
                        taxifiedRecords.Add(sellRecord);

                        break;
                    }
                case "transfer":
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = (record.AmountIn - record.AmountOut);
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate;
                        sellRecord.TransactionType = "sell";
                        sellRecord.IsNFT = false;
                        sellRecord.InternalNotes = "Transfer fees";
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(sellRecord);

                        break;
                    }
                case "sell":
                    {


                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate;
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
                        buyRecord.TransactionDate = record.TransactionDate;
                        buyRecord.TransactionType = "buy";
                        buyRecord.IsNFT = false;
                        //buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(buyRecord);
                        break;
                    }
                case "nftsell":
                    {
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoTransactionRecord();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn;
                        sellRecord.AmountAssetType = record.CurrencyIn;
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.Value = record.AmountOut;
                        sellRecord.ValueAssetType = record.CurrencyOut;
                        sellRecord.TransactionDate = record.TransactionDate;
                        sellRecord.TransactionType = "nftsell";
                        sellRecord.IsNFT = true;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        taxifiedRecords.Add(sellRecord);

                        var buyRecord = new CryptoTransactionRecord();
                        buyRecord.TaxableEvent = false;
                        buyRecord.Amount = record.AmountOut;
                        buyRecord.AmountAssetType = record.CurrencyOut;
                        buyRecord.Sequence = record.Sequence;
                        buyRecord.TransactionDate = record.TransactionDate;
                        buyRecord.TransactionType = "buy";
                        buyRecord.IsNFT = true;
                        //buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

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
                            TransactionDate = record.TransactionDate,
                            TransactionType = "buy",
                            IsNFT = false,
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate
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
        /// Augments non NFT crypto records with corresponding exchange rates and calculates total amounts
        /// </summary>
        /// <param name="records">Crypto records</param>
        /// <param name="exchangeRates">List of exchange rates to use for reference</param>
        /// <returns>Augmented list of (non NFT) crypto transactions</returns>
        public static List<CryptoTransactionRecord> AddExchangeRates(List<CryptoTransactionRecord> transactions, List<ExchangeRate> exchangeRates)
        {

            List<CryptoTransactionRecord> records = new List<CryptoTransactionRecord>();
            records.AddRange(transactions.Where(x => x.IsNFT == false));
            if (records.Count > 0)
            {
                List<CryptoTransactionRecord> result = new List<CryptoTransactionRecord>();
                if (records.Count > 2)
                {
                    return records.Select(x =>
                    {
                        x.InternalNotes = "Skipped, scenario not supported.";
                        return x;
                    }).ToList();
                }

                //init
                CryptoTransactionRecord updatedRecord;

                if (records.Count > 1)
                {
                    //sell & nftbuy & nftsell contain an in and out transaction; for which 
                    if (records.Any(x => x.TransactionType == "sell"))
                    {
                        //exchange rate for BUY will be inferred to be in line with the sell amount.
                        //Ie; if the sell is worth 100AUD the subsequent buy will be 100AUD

                        var sellRecord = records.First(x => x.TransactionType == "sell");
                        updatedRecord = new CryptoTransactionRecord();
                        updatedRecord.Sequence = sellRecord.Sequence;
                        updatedRecord.Value = sellRecord.Value;
                        updatedRecord.TransactionDate = sellRecord.TransactionDate.ToUniversalTime();
                        updatedRecord.ValueAssetType = sellRecord.ValueAssetType;
                        updatedRecord.Amount = sellRecord.Amount;
                        updatedRecord.AmountAssetType = sellRecord.AmountAssetType;
                        updatedRecord.IsNFT = sellRecord.IsNFT;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = sellRecord.TaxableEvent;
                        updatedRecord.InternalNotes = sellRecord.InternalNotes;

                        string sourceCurrency = sellRecord.AmountAssetType ?? string.Empty;
                        DateTime exchangeRateDay = sellRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime exchangeRateMaxOffset = exchangeRateDay.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                        string transactionType = sellRecord.TransactionType ?? string.Empty;
                        double exchangeRate = 0d;

                        //skip looking up exchange rate, if below conditions matches

                        

                        while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        {
                            //Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                            var exchangeRateInformation = (sellRecord.ExchangeRateValue is null || sellRecord.ExchangeRateValue == 0 ? exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay) : null);

                            if (exchangeRateInformation is not null && (!string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency)))
                            {
                                targetCurrency = exchangeRateInformation.ExchangeCurrency;

                                if (exchangeRate == 0d)
                                {
                                    exchangeRate = Convert.ToDouble(exchangeRateInformation.Low);
                                }
                                else
                                {
                                    var previousExchangeRate = exchangeRate;
                                    exchangeRate = previousExchangeRate * Convert.ToDouble(exchangeRateInformation.Low);

                                }

                                //high - low difference tolerance check
                            }
                            else
                            {
                                if (sellRecord.ExchangeRateValue == 0)
                                {
                                    if (exchangeRateDay >= exchangeRateMaxOffset)
                                    {
                                        //attempt to lookup exchange-rate for previous day
                                        exchangeRateDay = exchangeRateDay.AddDays(-1);
                                        updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                                      
                                    }
                                    else
                                    {
                                        updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                                        break; //exit while;
                                    }
                                    
                                   
                                }
                                else
                                {
                                    updatedRecord.UsesManualAssignedExchangeRate = true;
                                    exchangeRate = sellRecord.ExchangeRateValue ?? 0d;
                                }

                            }
                        }

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = transactionType;
                        updatedRecord.Value = (sellRecord.Amount * exchangeRate);
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }

                    if (records.Any(x => x.TransactionType == "buy"))
                    {
                        //exchange rate for BUY will be inferred to be in line with the sell amount.
                        //Ie; if the sell is worth 100AUD the subsequent buy will be 100AUD

                        var buyRecord = records.First(x => x.TransactionType == "buy");

                        updatedRecord = new CryptoTransactionRecord();

                        var buyAmountValue = result.First(x => x.TransactionType == "sell").Value;
                        var buyAmountCurrency = result.First(x => x.TransactionType == "sell").ExchangeRateCurrency;

                        updatedRecord.Sequence = buyRecord.Sequence;
                        updatedRecord.TransactionType = buyRecord.TransactionType;
                        updatedRecord.Value = buyAmountValue;
                        updatedRecord.TransactionDate = buyRecord.TransactionDate.ToUniversalTime();
                        updatedRecord.ValueAssetType = buyAmountCurrency;
                        updatedRecord.Amount = buyRecord.Amount;
                        updatedRecord.AmountAssetType = buyRecord.AmountAssetType;
                        updatedRecord.IsNFT = buyRecord.IsNFT;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = buyRecord.TaxableEvent;
                        updatedRecord.InternalNotes = buyRecord.InternalNotes;
                        updatedRecord.ExchangeRateCurrency = buyAmountCurrency;
                        updatedRecord.ExchangeRateValue = (buyAmountValue / buyRecord.Amount);

                        result.Add(updatedRecord);
                        // 500 xrp   500 csc  --> 1000 AUD value
                        //updatedRecord.Value = (sellRecord.Amount * exchangeRate);



                    }
                }
                else
                {
                    var record = records.First();
                    //foreach (var record in records)
                    //{
                    updatedRecord = new CryptoTransactionRecord();

                    updatedRecord.Sequence = record.Sequence;
                    updatedRecord.Value = record.Value;
                    updatedRecord.TransactionDate = record.TransactionDate.ToUniversalTime();
                    updatedRecord.ValueAssetType = record.ValueAssetType;
                    updatedRecord.Amount = record.Amount;
                    updatedRecord.AmountAssetType = record.AmountAssetType;
                    updatedRecord.IsNFT = record.IsNFT;
                    updatedRecord.UsesManualAssignedExchangeRate = false;
                    updatedRecord.TaxableEvent = record.TaxableEvent;
                    updatedRecord.InternalNotes = record.InternalNotes;

                    string sourceCurrency = record.AmountAssetType ?? string.Empty;
                    DateTime exchangeRateDay = record.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                    DateTime exchangeRateMaxOffset = exchangeRateDay.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day

                    string targetCurrency = sourceCurrency; // string.Empty;
                    string transactionType = record.TransactionType ?? string.Empty;
                    double exchangeRate = 0d;

                    while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                    {
                        // Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                        var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay);
                        //var exchangeRateInformation = (record.ExchangeRateValue is null || record.ExchangeRateValue == 0 ? exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay) : null);


                        if (exchangeRateInformation is not null && (!string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency)))
                        {
                            targetCurrency = exchangeRateInformation.ExchangeCurrency;
                            double low = Convert.ToDouble(exchangeRateInformation.Low);
                            double high = Convert.ToDouble(exchangeRateInformation.High);
                            bool flagHighDailyPriceFlux = false;


                            if (exchangeRate == 0d)
                            {
                                exchangeRate = (transactionType == "buy" ? Convert.ToDouble(exchangeRateInformation.High) : Convert.ToDouble(exchangeRateInformation.Low));
                            }
                            else
                            {
                                var previousExchangeRate = exchangeRate;
                                exchangeRate = previousExchangeRate * (transactionType == "buy" ? Convert.ToDouble(exchangeRateInformation.High) : Convert.ToDouble(exchangeRateInformation.Low));

                            }

                            //high - low difference tolerance check
                        }
                        else
                        {
                            if (record.ExchangeRateValue == 0)
                            {
                                if (exchangeRateDay >= exchangeRateMaxOffset)
                                {
                                    //attempt to lookup exchange-rate for previous day
                                    exchangeRateDay = exchangeRateDay.AddDays(-1);
                                    updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                                                            }
                                else
                                {
                                    updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                                    break; //exit while;
                                }
                                
                            }
                            else
                            {
                                updatedRecord.UsesManualAssignedExchangeRate = true;
                                exchangeRate = record.ExchangeRateValue ?? 0d;
                            }

                        }
                    }

                    //update exchange rates

                    updatedRecord.ExchangeRateValue = exchangeRate;
                    updatedRecord.ExchangeRateCurrency = targetCurrency;
                    updatedRecord.TransactionType = transactionType;
                    updatedRecord.Value = (record.Amount * exchangeRate);
                    updatedRecord.ValueAssetType = targetCurrency;

                    result.Add(updatedRecord);

                }

                return result;
            }
            else
            {
                return null;
            }

        }

        /// <summary>
        /// Augments NFT crypto records with corresponding exchange rates and calculates total amounts
        /// </summary>
        /// <param name="records">Crypto records</param>
        /// <param name="exchangeRates">List of exchange rates to use for reference</param>
        /// <returns>Augmented list of NFT crypto transactions</returns>
        public static List<CryptoTransactionRecord> AddNFTExchangeRates(List<CryptoTransactionRecord> transactions, List<ExchangeRate> exchangeRates)
        {


            List<CryptoTransactionRecord> records = new List<CryptoTransactionRecord>();
            records.AddRange(transactions.Where(x => x.IsNFT == true));
            if (records.Count > 0)
            {
                List<CryptoTransactionRecord> result = new List<CryptoTransactionRecord>();
                if (records.Count > 2)
                {
                    return records.Select(x =>
                    {
                        x.InternalNotes = "Skipped, scenario not supported.";
                        return x;
                    }).ToList();
                }

                //init
                CryptoTransactionRecord updatedRecord;

                if (records.Count > 1)
                {
                    //NFTBUY Logic
                    if (records.Any(x => x.TransactionType == "sell"))
                    {
                        updatedRecord = new CryptoTransactionRecord();
                        var sellNFTRecord = records.First(x => x.TransactionType == "sell");

                        updatedRecord.Sequence = sellNFTRecord.Sequence;
                        updatedRecord.Value = sellNFTRecord.Value;
                        updatedRecord.ValueAssetType = sellNFTRecord.ValueAssetType;
                        updatedRecord.TransactionDate = sellNFTRecord.TransactionDate.ToUniversalTime();

                        updatedRecord.Amount = sellNFTRecord.Amount;
                        updatedRecord.AmountAssetType = sellNFTRecord.AmountAssetType;
                        updatedRecord.IsNFT = false;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = sellNFTRecord.TaxableEvent;
                        updatedRecord.InternalNotes = sellNFTRecord.InternalNotes;

                        string sourceCurrency = sellNFTRecord.AmountAssetType ?? string.Empty;
                        DateTime exchangeRateDay = sellNFTRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime exchangeRateMaxOffset = exchangeRateDay.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                        string transactionType = sellNFTRecord.TransactionType ?? string.Empty;
                        double exchangeRate = 0d;

                        while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        {
                            //Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                            var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay);

                            if (exchangeRateInformation is not null && (!string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency)))
                            {
                                targetCurrency = exchangeRateInformation.ExchangeCurrency;

                                if (exchangeRate == 0d)
                                {
                                    exchangeRate = Convert.ToDouble(exchangeRateInformation.Low);
                                }
                                else
                                {
                                    var previousExchangeRate = exchangeRate;
                                    exchangeRate = previousExchangeRate * Convert.ToDouble(exchangeRateInformation.Low);

                                }

                                //high - low difference tolerance check
                            }
                            else
                            {
                                if (sellNFTRecord.ExchangeRateValue == 0)
                                {
                                    if (exchangeRateDay >= exchangeRateMaxOffset)
                                    {
                                        //attempt to lookup exchange-rate for previous day
                                        exchangeRateDay = exchangeRateDay.AddDays(-1);
                                        updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                                                                }
                                    else
                                    {
                                        updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                                        break; //exit while;
                                    }
                                    
                                }
                                else
                                {
                                    updatedRecord.UsesManualAssignedExchangeRate = true;
                                    exchangeRate = sellNFTRecord.ExchangeRateValue ?? 0d;
                                }

                            }
                        }

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = transactionType;
                        updatedRecord.Value = (sellNFTRecord.Amount * exchangeRate);
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }
                    if (records.Any(x => x.TransactionType == "nftbuy"))
                    {
                        var sellRecord = result.First();

                        var buyNFTRecord = records.First(x => x.TransactionType == "nftbuy");
                        updatedRecord = new CryptoTransactionRecord();
                        updatedRecord.Sequence = buyNFTRecord.Sequence;
                        updatedRecord.Value = sellRecord.Value;
                        updatedRecord.ValueAssetType = sellRecord.ValueAssetType;
                        updatedRecord.TransactionDate = buyNFTRecord.TransactionDate.ToUniversalTime();

                        updatedRecord.Amount = buyNFTRecord.Amount;
                        updatedRecord.AmountAssetType = buyNFTRecord.AmountAssetType;
                        updatedRecord.IsNFT = buyNFTRecord.IsNFT;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = buyNFTRecord.TaxableEvent;
                        updatedRecord.InternalNotes = buyNFTRecord.InternalNotes;
                        updatedRecord.TransactionType = "buy";
                        updatedRecord.ExchangeRateValue = (sellRecord.Value / buyNFTRecord.Amount);
                        updatedRecord.ExchangeRateCurrency = sellRecord.ExchangeRateCurrency;

                        result.Add(updatedRecord);
                    }

                    //NFTSELL Logic
                    if (records.Any(x => x.TransactionType == "buy"))
                    {
                        updatedRecord = new CryptoTransactionRecord();
                        var sellNFTRecord = records.First(x => x.TransactionType == "buy");

                        updatedRecord.Sequence = sellNFTRecord.Sequence;
                        updatedRecord.Value = sellNFTRecord.Value;
                        updatedRecord.ValueAssetType = sellNFTRecord.ValueAssetType;
                        updatedRecord.TransactionDate = sellNFTRecord.TransactionDate.ToUniversalTime();

                        updatedRecord.Amount = sellNFTRecord.Amount;
                        updatedRecord.AmountAssetType = sellNFTRecord.AmountAssetType;
                        updatedRecord.IsNFT = false;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = sellNFTRecord.TaxableEvent;
                        updatedRecord.InternalNotes = sellNFTRecord.InternalNotes;

                        string sourceCurrency = sellNFTRecord.AmountAssetType ?? string.Empty;
                        DateTime exchangeRateDay = sellNFTRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime exchangeRateMaxOffset = exchangeRateDay.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                        string transactionType = sellNFTRecord.TransactionType ?? string.Empty;
                        double exchangeRate = 0d;

                        while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        {
                            //Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                            var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay);

                            if (exchangeRateInformation is not null && (!string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency)))
                            {
                                targetCurrency = exchangeRateInformation.ExchangeCurrency;

                                if (exchangeRate == 0d)
                                {
                                    exchangeRate = Convert.ToDouble(exchangeRateInformation.Low);
                                }
                                else
                                {
                                    var previousExchangeRate = exchangeRate;
                                    exchangeRate = previousExchangeRate * Convert.ToDouble(exchangeRateInformation.Low);

                                }

                                //high - low difference tolerance check
                            }
                            else
                            {
                                if (sellNFTRecord.ExchangeRateValue == 0)
                                {
                                    if (exchangeRateDay >= exchangeRateMaxOffset)
                                    {
                                        //attempt to lookup exchange-rate for previous day
                                        exchangeRateDay = exchangeRateDay.AddDays(-1);
                                        updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                                                                }
                                    else
                                    {
                                        updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                                        break; //exit while;
                                    }
                                    
                                }
                                else
                                {
                                    updatedRecord.UsesManualAssignedExchangeRate = true;
                                    exchangeRate = sellNFTRecord.ExchangeRateValue ?? 0d;
                                }

                            }
                        }

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = transactionType;
                        updatedRecord.Value = (sellNFTRecord.Amount * exchangeRate);
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }
                    if (records.Any(x => x.TransactionType == "nftsell"))
                    {
                        var sellRecord = result.First();

                        var buyNFTRecord = records.First(x => x.TransactionType == "nftsell");
                        
                        updatedRecord = new CryptoTransactionRecord();
                        updatedRecord.Sequence = buyNFTRecord.Sequence;
                        updatedRecord.Value = sellRecord.Value;
                        updatedRecord.ValueAssetType = buyNFTRecord.ExchangeRateCurrency;//sellRecord.ValueAssetType;
                        updatedRecord.TransactionDate = buyNFTRecord.TransactionDate.ToUniversalTime();

                        updatedRecord.Amount = buyNFTRecord.Amount;
                        updatedRecord.AmountAssetType = buyNFTRecord.AmountAssetType;
                        updatedRecord.IsNFT = buyNFTRecord.IsNFT;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = buyNFTRecord.TaxableEvent;
                        updatedRecord.InternalNotes = buyNFTRecord.InternalNotes;
                        updatedRecord.TransactionType = "sell";
                        updatedRecord.ExchangeRateValue = (sellRecord.Value / buyNFTRecord.Amount);
                        updatedRecord.ExchangeRateCurrency = buyNFTRecord.ExchangeRateCurrency; //sellRecord.ExchangeRateCurrency;

                        result.Add(updatedRecord);
                    }
                }


                return result.AsEnumerable().Reverse().ToList();
            }
            else
            {
                return null;
            }

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
            foreach (var dailyTransactions in records.GroupBy(x => x.TransactionDate.Date))
            {
                //group buys/sells and consolidate
                result.AddRange(ConsolidateTransactions(dailyTransactions.Where(x => x.TransactionType == "buy").ToList()));
                result.AddRange(ConsolidateTransactions(dailyTransactions.Where(x => x.TransactionType == "sell").ToList()));

            }

            //sort by buys 
            result = result.OrderBy(x => x.TransactionType).ThenBy(x => x.TransactionDate.Date).ToList();



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
            List<CryptoTransactionRecord> results = new();
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
                foreach (var matchedCollections in shortlistedCollections.OrderByDescending(x => x.BoughtAt))
                {
                    //check if current item has sufficient funds
                    if ((matchedCollections.Available - sellAmount) >= 0)
                    {
                        //sufficient
                        DateTime endOfCurrentFinancialYear = new DateTime(2022, 6, 30);
                        //bool useRebate = (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(endOfCurrentFinancialYear));

                        CryptoTaxRecords TaxRecord = new CryptoTaxRecords();

                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Name = matchedCollections.Name;
                        TaxRecord.SellAmount = sellAmount ?? 0d;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0d;

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            TaxRecord.CapitalGainAmount = (((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * sellAmount ?? 0d) * 0.5;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {sellAmount ?? 0d}) * 0.5 (taxdiscount)";
                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * sellAmount ?? 0d;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {sellAmount ?? 0d}";
                        }

                        result.Add(TaxRecord);
                       



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

                        CryptoTaxRecords TaxRecord = new CryptoTaxRecords();

                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Name = matchedCollections.Name;
                        TaxRecord.SellAmount = availableAmount;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0d;

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            TaxRecord.CapitalGainAmount = (((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * availableAmount ) * 0.5;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {availableAmount}) * 0.5 (taxdiscount)";
                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * availableAmount;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {availableAmount}";
                        }

                        result.Add(TaxRecord);

                        //update sell amount remaining
                        sellAmount = sellAmount - availableAmount;

                        //update collection
                        matchedCollections.Available = 0d;
                    }
                }

                if (sellAmount > 0)
                {
                    //there is an error; as we are trying to sell whilst there are no funds available.
                    taxableTransactions.InternalNotes = $"Invalid transactions, insufficient funds to sell. Missing {sellAmount} {taxableTransactions?.AmountAssetType?.ToLower()} ";
                }
            }

            return result;
        }

        public static List<CryptoTaxRecords> CreateCryptoTaxRecords(List<CryptoCollection> cryptoCollection, List<CryptoTransactionRecord> records, List<ExchangeRate> exchangeRates, int endYear)
        {           
            var endDate = new DateTime(endYear, 7, 1);

            //we will be leveraging High-in First-out (HIFO)
            var result = new List<CryptoTaxRecords>();
            foreach (var taxableTransactions in records.Where(x => x.TransactionDate < endDate &&   x.TaxableEvent).OrderBy(x => x.TransactionDate))
            {
                //retrieve sell exchange rate
                var sellExchangeRate = Convert.ToDouble(exchangeRates.FirstOrDefault(x => x.Date == taxableTransactions.TransactionDate && x.Symbol?.ToLower() == taxableTransactions?.AmountAssetType?.ToLower())?.Low);

                //lookup HIFO entry
                var shortlistedCollections = cryptoCollection.Where(x => x.CreatedOn <= DateOnly.FromDateTime(taxableTransactions.TransactionDate) && x.Name.ToLower() == taxableTransactions?.AmountAssetType?.ToLower() && x.Available > 0);
                var sellAmount = taxableTransactions.Amount;
                foreach (var matchedCollections in shortlistedCollections.OrderByDescending(x => x.BoughtAt))
                {
                    //check if current item has sufficient funds
                    if ((matchedCollections.Available - sellAmount) >= 0)
                    {
                        //sufficient
                        DateTime endOfCurrentFinancialYear = new DateTime(2022, 6, 30);
                        //bool useRebate = (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(endOfCurrentFinancialYear));

                        CryptoTaxRecords TaxRecord = new CryptoTaxRecords();

                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Name = matchedCollections.Name;
                        TaxRecord.SellAmount = sellAmount ?? 0d;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0d;

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            TaxRecord.CapitalGainAmount = (((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * sellAmount ?? 0d) * 0.5;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {sellAmount ?? 0d}) * 0.5 (taxdiscount)";
                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * sellAmount ?? 0d;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {sellAmount ?? 0d}";
                        }

                        result.Add(TaxRecord);




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

                        CryptoTaxRecords TaxRecord = new CryptoTaxRecords();

                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Name = matchedCollections.Name;
                        TaxRecord.SellAmount = availableAmount;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0d;

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            TaxRecord.CapitalGainAmount = (((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * availableAmount) * 0.5;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {availableAmount}) * 0.5 (taxdiscount)";
                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0d) - matchedCollections.BoughtAt) * availableAmount;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0d}) - {matchedCollections.BoughtAt}) * {availableAmount}";
                        }

                        result.Add(TaxRecord);

                        //update sell amount remaining
                        sellAmount = sellAmount - availableAmount;

                        //update collection
                        matchedCollections.Available = 0d;
                    }
                }

                if (sellAmount > 0)
                {
                    //there is an error; as we are trying to sell whilst there are no funds available.
                    taxableTransactions.InternalNotes = $"Invalid transactions, insufficient funds to sell. Missing {sellAmount} {taxableTransactions?.AmountAssetType?.ToLower()} ";
                }
            }

            return result;
        }
    }

}
