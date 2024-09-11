using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Polly;
using Shared.Enums;
using Shared.Models;
using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ExchangeRateManagerAPI.Services
{
    public class DatabaseService
    {

        private readonly ILogger<DatabaseService> _logger;
        private readonly IConfiguration _config;
        private readonly IDbContextFactory<CryptoTaxManDbContext> _dbContextFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly IDictionary<string, TaskCompletionSource<(int records, string? message, bool error, object?)>> _tasksProcessCryptoUserTransactionsStaging = new Dictionary<string, TaskCompletionSource<(int records, string? message, bool error, object?)>>();
        private readonly IDictionary<string, Exception> _taskExceptions = new Dictionary<string, Exception>();
        public string StatusMessage = string.Empty;

        public DatabaseService(ILogger<DatabaseService> logger, IConfiguration config, IWebHostEnvironment environment, IDbContextFactory<CryptoTaxManDbContext> dbContextFactory) // CryptoTaxManDbContext dbContext)
        {
            _logger = logger;
            _config = config;
            _environment = environment;
            _dbContextFactory = dbContextFactory;

        }


        public async Task<ExchangeRate?> FetchExchangeRateInformation(DateTime transactionDate, string currencyIn, string exchangeCurrency)
        {
            try
            {

                var compositeKey = new object[] { transactionDate, currencyIn, exchangeCurrency };

                using var dbContext = _dbContextFactory.CreateDbContext();
                var result = await dbContext.ExchangeRates.FindAsync(compositeKey);
                if (result is not null)
                {
                    return result;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"{transactionDate}, {currencyIn},{exchangeCurrency} caused error {ex.Message}");
            }

            return null;

        }


        public async Task<(int records, string message, bool error)> InsertCryptoUserTransactionsStaging(List<CryptoUserTransactionStaging> transactions)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                await dbContext.CryptoUserTransactionsStaging.AddRangeAsync(transactions);
                return (await dbContext.SaveChangesAsync(), "Inserted", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"InsertCryptoUserTransactions caused error {ex.Message}");
                return (0, $"InsertCryptoUserTransactions caused error {ex.Message}", true);
            }


        }

        public async Task<(int records, string message, bool error)> UpsertCryptoUserTranssactionsStaging(List<CryptoUserTransactionStaging> transactions)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var existingRecords = await dbContext.CryptoUserTransactionsStaging.ToListAsync();
                var newRecords = transactions.Where(x => !existingRecords.Select(y => y.Sequence).Contains(x.Sequence)).ToList();
                var updatedRecords = transactions.Where(x => existingRecords.Select(y => y.Sequence).Contains(x.Sequence)).ToList();

                dbContext.CryptoUserTransactionsStaging.UpdateRange(updatedRecords);
                await dbContext.CryptoUserTransactionsStaging.AddRangeAsync(newRecords);
                return (await dbContext.SaveChangesAsync(), "Upserted", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpsertCryptoUserTranssactionsStaging caused error {ex.Message}");
                return (0, $"UpsertCryptoUserTranssactionsStaging caused error {ex.Message}", true);
            }
        }



        public string StartProcessCryptoUserTransactionsStagingTask(Func<Task<(int records, string? message, bool error, object? details)>> taskFunc)
        {
            var taskId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<(int records, string? message, bool error, object? details)>();
            _tasksProcessCryptoUserTransactionsStaging.Add(taskId, tcs);

            Task.Run(async () =>
            {
                try
                {
                    var result = await taskFunc();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                    lock (_taskExceptions)
                    {
                        _taskExceptions[taskId] = ex;
                    }
                }
            });

            return taskId;
        }




        public async Task<(int records, string? message, bool error, object? details)> ProcessCryptoUserTransactionsStaging(string baseExchangeCurrency = "aud")
        {
            int recordsAffected = 0;
            InvalidCryptoUserTransaction? invalidTransactions = null;
            decimal exchangeRateAllowedFluctuation = _config.GetValue<decimal>("exchangeRateFluctuationThresholdPercentage", 100m);
            string responseMessage = string.Empty;
            bool errorOccured = false;
            try
            {
                List<CryptoUserTransaction> transactions = new List<CryptoUserTransaction>();
                using (var dbContext = _dbContextFactory.CreateDbContext())
                {

                    var stagingRecords = await dbContext.CryptoUserTransactionsStaging.Where(x => x.IsProcessed == false).OrderBy(o => o.TransactionDate).ToListAsync();

                    //return if stagingRecords is null or empty
                    if (stagingRecords is null || stagingRecords.Count == 0)
                    {
                        responseMessage = "No transactions to process";
                        return (recordsAffected, responseMessage, errorOccured, invalidTransactions);
                    }

                    //flatten the records

                    foreach (var record in stagingRecords)
                    {
                        var result = PrepareCryptoUserTransactions(record);
                        if (result is not null && result.Count > 0)
                        {
                            transactions.AddRange(result);
                        }
                        //await dbContext.CryptoUserTransactions.AddRangeAsync(transactions);
                        record.IsProcessed = true;
                    }

                    if (transactions is null || transactions.Count == 0)
                    {
                        responseMessage = "No transactions to process";
                        return (recordsAffected, responseMessage, errorOccured, invalidTransactions);
                    }

                    //check for invald transactions                
                    invalidTransactions = ValidateCryptoTransactionRecords(transactions);

                    if (invalidTransactions is not null)
                    {
                        //dispose our context
                        await dbContext.DisposeAsync();
                        errorOccured = true;
                        responseMessage = "Invalid transactions found";
                        return (recordsAffected, responseMessage, errorOccured, invalidTransactions);
                    }
                    else
                    {
                        //dispose our context
                        dbContext.SaveChanges();
                        await dbContext.DisposeAsync();
                    }
                }


                //now lookup exchange rates from the exchange rate table for each transaction
                int counter = 0;
                foreach (var transaction in transactions)
                {
                    StatusMessage = $"Processing transaction {counter++} of {transactions.Count}";
                    // if (transaction.TransactionDate is null)
                    // {
                    //     continue;
                    // }


                    ExchangeRate? exchangeRate = null;
                    //check if we have an exchange rate for the transaction date, if not then we need to look up the previous day and go back up to 7 days
                    //while(true)
                    //{
                    DateTime transactionDate = transaction.TransactionDate; // ?? DateTime.MaxValue;

                    //format the transactionDate to exclude the time
                    transactionDate = new DateTime(transactionDate.Year, transactionDate.Month, transactionDate.Day, 0, 0, 0, DateTimeKind.Utc);
                    //exchangeRate = await FetchExchangeRateInformation(transactionDate, transaction.AmountAssetType, baseExchangeCurrency);


                    int maxTimeTravelAllowed = _config.GetValue<int>("maxNearestMatchRange", 1);
                    do
                    {
                        exchangeRate = await FetchExchangeRateInformation(transactionDate, transaction.AmountAssetType.ToLower(), baseExchangeCurrency.ToLower());// await _databaseService.FetchExchangeRateInformation(transactionDate, currencyIn.ToLower(), exchangeCurrency.ToLower());
                        if (exchangeRate is not null)
                        {
                            break;
                        }

                        if (exchangeRate is null)
                        {
                            if (maxTimeTravelAllowed == 0)
                            {
                                break;
                            }
                            transactionDate = transactionDate.AddDays(-1);
                            maxTimeTravelAllowed--;
                        }
                    } while (true);


                    //we are going to enhance this piece of code with logic which will check for huge fluctuations in exchange rates and if found we will flag the transaction for manual review
                    
                    if (exchangeRate is not null)
                    {

                        //check for huge fluctuations in exchange rates
                        if(Math.Abs(exchangeRate.OpenClosePercentageDifference??0m) > exchangeRateAllowedFluctuation || Math.Abs(exchangeRate.LowHighPercentageDifference ?? 0m) > exchangeRateAllowedFluctuation)
                        {
                            transaction.ReviewRequired = true;
                            transaction.Approved = false;
                           
                        }

                        //if transaction type is a buy then use the highest rate choose between the open/close average and the close rate to satisfy tax office conditions
                        if (transaction.TransactionType == Shared.Enums.TransactionEventType.buy)
                        {
                            decimal buyExchangeRateToUse = 0m;
                            //in case that the buy is registed as additional income, then we will use the lowest buy rate on a given day to optimise for tax purposes
                            if (transaction.ReportableAsIncome)
                            {
                               
                                    //determine which rate to use; choose between OpenCloseAverage, LowHighAverage and Close
                                    buyExchangeRateToUse = Math.Min(exchangeRate.OpenCloseAverage, Math.Min(exchangeRate.LowHighAverage, exchangeRate.Close));
                               
                            }
                            else
                            {
                                //determine which rate to use; choose between OpenCloseAverage, LowHighAverage and Close
                                 buyExchangeRateToUse = Math.Max(exchangeRate.OpenCloseAverage, Math.Max(exchangeRate.LowHighAverage, exchangeRate.Close));
                            }

                            //if(transaction.Approved == false)
                            //{
                            //    buyExchangeRateToUse = 0m;
                            //}

                           

                            transaction.ExchangeRateValue = buyExchangeRateToUse;
                            transaction.ExchangeRateCurrency = exchangeRate.ExchangeCurrency;
                            transaction.Value = transaction.Amount * buyExchangeRateToUse;

                            string exchangeRateUsed;

                            if (buyExchangeRateToUse == exchangeRate.OpenCloseAverage)
                            {
                                exchangeRateUsed = nameof(exchangeRate.OpenCloseAverage);
                            }
                            else if (buyExchangeRateToUse == exchangeRate.LowHighAverage)
                            {
                                exchangeRateUsed = nameof(exchangeRate.LowHighAverage);
                            }
                            else
                            {
                                exchangeRateUsed = nameof(exchangeRate.Close);
                            }

                            if (transaction.ReviewRequired)
                            {
                                transaction.InternalNotes = $"Exchange rate fluctuation exceeds threshold. OCT: {exchangeRate.OpenClosePercentageDifference} LHT: {exchangeRate.LowHighPercentageDifference}, manual review required. (Exchange rate used: {exchangeRateUsed})";
                            }
                            else
                            {
                                transaction.InternalNotes = $"Exchange rate used: {exchangeRateUsed}";
                            }

                        }
                        //transaction is sell so we choose the lowest rate between open/close average and the close rate to satisfy tax office conditions
                        else
                        {
                            var sellExchangeRateToUse = Math.Min(exchangeRate.OpenCloseAverage, Math.Min(exchangeRate.LowHighAverage, exchangeRate.Close));

                            transaction.ExchangeRateValue = sellExchangeRateToUse;
                            transaction.ExchangeRateCurrency = exchangeRate.ExchangeCurrency;
                            transaction.Value = transaction.Amount * sellExchangeRateToUse;

                            string exchangeRateUsed;

                            if (sellExchangeRateToUse == exchangeRate.OpenCloseAverage)
                            {
                                exchangeRateUsed = nameof(exchangeRate.OpenCloseAverage);
                            }
                            else if (sellExchangeRateToUse == exchangeRate.LowHighAverage)
                            {
                                exchangeRateUsed = nameof(exchangeRate.LowHighAverage);
                            }
                            else
                            {
                                exchangeRateUsed = nameof(exchangeRate.Close);
                            }

                            if (transaction.ReviewRequired)
                            {
                                transaction.InternalNotes = $"Exchange rate fluctuation exceeds threshold. OCT: {exchangeRate.OpenClosePercentageDifference} LHT: {exchangeRate.LowHighPercentageDifference}, manual review required. (Exchange rate used: {exchangeRateUsed})";
                            }
                            else
                            {
                                transaction.InternalNotes = $"Exchange rate used: {exchangeRateUsed}";
                            }


                        }



                    }
                    else
                    {
                        // transaction.UsesManualAssignedExchangeRate = true;
                        transaction.ExchangeRateValue = 0m;
                        transaction.ExchangeRateCurrency = baseExchangeCurrency;
                        transaction.Value = 0m;
                    }
                }


                //now group CryptoUserTransactions by sequence where the group count is exactly 2 (sell.buy or buy.sell)
                // var groupedTx = transactions.GroupBy(x => x.Sequence).Where(x => x.Count() == 2).ToList();

                //now sort the transactions by sequence and call UpsertCryptoUserTranssactions
                var sortedTransactions = transactions.OrderBy(x => x.Sequence).ToList();
                var upsertResult = await UpsertCryptoUserTransactions(sortedTransactions);

                return (upsertResult.records, upsertResult.message, upsertResult.error, invalidTransactions);




            }

            catch (Exception ex)
            {
                _logger.LogError($"ProcessCryptoUserTransactionsStaging caused error {ex.Message}");
                responseMessage = $"ProcessCryptoUserTransactionsStaging caused error {ex.Message}";
                errorOccured = true;

            }

            return (recordsAffected, responseMessage, errorOccured, invalidTransactions);
        }


        public (bool IsCompleted, (int records, string? message, bool error, object? details) Result, Exception Error) CheckProcessCryptoUserTransactionsStagingTaskStatus(string taskId)
        {
            if (_tasksProcessCryptoUserTransactionsStaging.TryGetValue(taskId, out var tcs))
            {
                if (tcs.Task.IsCompleted)
                {
                    _taskExceptions.TryGetValue(taskId, out var exception);
                    return (true, tcs.Task.Result, exception);
                }
            }

            return (false, (0, string.Empty, false, null), null);
        }
        public async Task<(int records, string message, bool error)> UpsertCryptoUserTransactions(List<CryptoUserTransaction> transactions)
        {
            try
            {

                using var dbContext = _dbContextFactory.CreateDbContext();
                var existingRecords = await dbContext.CryptoUserTransactions.ToListAsync();


                //now update the existing records with the new values
                foreach (var record in transactions)
                {
                    var existingRecord = existingRecords.FirstOrDefault(x => x.Sequence == record.Sequence && x.TransactionDate == record.TransactionDate && x.TransactionType == record.TransactionType);
                    if (existingRecord is not null)
                    {
                        existingRecord.Amount = record.Amount;
                        existingRecord.AmountAssetType = record.AmountAssetType;
                        existingRecord.ExchangeRateCurrency = record.ExchangeRateCurrency;
                        existingRecord.ExchangeRateValue = record.ExchangeRateValue;
                        existingRecord.IsNFT = record.IsNFT;
                        existingRecord.TaxableEvent = record.TaxableEvent;
                        existingRecord.Value = record.Value;
                        existingRecord.ValueAssetType = record.ValueAssetType;
                        existingRecord.UsesManualAssignedExchangeRate = record.UsesManualAssignedExchangeRate;
                        existingRecord.InternalNotes = record.InternalNotes;
                    }
                    else
                    {
                        await dbContext.CryptoUserTransactions.AddAsync(record);
                    }
                }


                //var newRecords = transactions
                //    .Where(x => !existingRecords
                //    .Select(y => new { y.Sequence, y.TransactionDate, y.TransactionType })
                //    .Contains(new { x.Sequence, x.TransactionDate, x.TransactionType }))
                //    .ToList();


                //var updatedRecords = transactions
                //    .Where(x => existingRecords
                //    .Select(y => new { y.Sequence, y.TransactionDate, y.TransactionType })
                //    .Contains(new { x.Sequence, x.TransactionDate, x.TransactionType }))
                //    .ToList();

                //var comparer = EqualityComparer<CryptoUserTransaction>.Default;

                //updatedRecords = updatedRecords.Except(newRecords, comparer).ToList();
                //newRecords = newRecords.Except(updatedRecords, comparer).ToList();


                //dbContext.CryptoUserTransactions.UpdateRange(updatedRecords);
                //await dbContext.CryptoUserTransactions.AddRangeAsync(newRecords);
                return (await dbContext.SaveChangesAsync(), "Upserted", false);

                //dbContext.CryptoUserTransactionsStaging.UpdateRange(updatedRecords);
                //await dbContext.CryptoUserTransactionsStaging.AddRangeAsync(newRecords);
                //return (await dbContext.SaveChangesAsync(), "Upserted", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpsertCryptoUserTransactions caused error {ex.Message}");
                return (0, $"UpsertCryptoUserTransactions caused error {ex.Message}", true);
            }
        }

        public async Task<(int records, string message, bool error)> InsertTradingPairInformation(List<ExchangeInformation> exchangeInformation)
        {
            try
            {
                //project exchangeInformation into TradingPairInformation
                var transactions = exchangeInformation.Select(x => new TradingPairInformation()
                {
                    IsActive = true,
                    ExchangeCurrency = x.ExchangeCurrency,
                    ExchangeName = x.ExchangeName,
                    ExchangeSymbol = x.ExchangeSymbol,
                    Kind = x.Kind,
                    Symbol = x.Symbol,
                    LastExchangeRateEntryDate = null,
                    SyncedOn = null                    
                }).ToList();

                using var dbContext = _dbContextFactory.CreateDbContext();

                //check for duplicates using the PK Symbol and EchangeCurrency and if found remove and only insert the new ones              
                var duplicates = await dbContext.TradingPairInformation.ToListAsync();
                transactions.RemoveAll(x => duplicates.Select(y => y.TradingPairName).Contains(x.TradingPairName));

                await dbContext.TradingPairInformation.AddRangeAsync(transactions);
                return (await dbContext.SaveChangesAsync(), "Inserted", false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"InsertTradingPairInformation caused error {ex.Message}");
                return (0, $"InsertTradingPairInformation caused error {ex.Message}", true);
            }

        }


        private static List<CryptoUserTransaction> PrepareCryptoUserTransactions(CryptoUserTransactionStaging record)
        {
            List<CryptoUserTransaction> flattenRecords = new List<CryptoUserTransaction>();

            switch (record.TransactionEvent)
            {

                case Shared.Enums.TransactionEventType.buy:
                    {
                        flattenRecords.Add(new CryptoUserTransaction
                        {
                            TaxableEvent = false,
                            Amount = record.AmountIn ?? 0m,
                            AmountAssetType = record.CurrencyIn?.ToLower(),
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate ?? DateTime.MinValue,
                            TransactionType = Shared.Enums.TransactionEventType.buy,
                            ReportableAsIncome = record.ReportableAsIncome ?? false,
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate,
                            IsNFT = false
                        });
                        break;
                    }
                case Shared.Enums.TransactionEventType.nftbuy:
                    {
                        //sell and a buy

                        flattenRecords.Add(new CryptoUserTransaction
                        {
                            TaxableEvent = true,
                            Amount = record.AmountIn ?? 0m,
                            AmountAssetType = record.CurrencyIn?.ToLower(),
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate ?? DateTime.MinValue,
                            TransactionType = Shared.Enums.TransactionEventType.sell,
                            //ExchangeRateCurrency = record.ExchangeCurrency,
                            //ExchangeRateValue = record.ExchangeRate,
                            IsNFT = true
                        });


                        flattenRecords.Add(new CryptoUserTransaction
                        {
                            TaxableEvent = false,
                            Amount = record.AmountOut ?? 0m,
                            AmountAssetType = record.CurrencyOut?.ToLower(),
                            Sequence = record.Sequence,
                            Value = record.AmountIn,
                            ValueAssetType = record.CurrencyIn?.ToLower(),
                            TransactionDate = record.TransactionDate ?? DateTime.MinValue,
                            TransactionType = Shared.Enums.TransactionEventType.nftbuy,
                            IsNFT = true

                        });
                        break;
                    }
                case Shared.Enums.TransactionEventType.stake:
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a STAKE SELL reflects the exchange rate staked against 
                        var sellRecord = new CryptoUserTransaction();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn ?? 0m;
                        sellRecord.AmountAssetType = record.CurrencyIn?.ToLower();
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                        sellRecord.TransactionType = Shared.Enums.TransactionEventType.sell;
                        sellRecord.IsNFT = false;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);
                        flattenRecords.Add(sellRecord);

                        break;
                    }
                case Shared.Enums.TransactionEventType.transfer:
                    {
                        //sell
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}
                        if (record.AmountIn - record.AmountOut > 0)
                        {
                            var sellRecord = new CryptoUserTransaction();


                            sellRecord.TaxableEvent = true;
                            sellRecord.Amount = (record.AmountIn ?? 0m) - (record.AmountOut ?? 0m);
                            sellRecord.AmountAssetType = record.CurrencyIn?.ToLower();
                            sellRecord.Sequence = record.Sequence;
                            sellRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                            sellRecord.TransactionType = Shared.Enums.TransactionEventType.sell;
                            sellRecord.IsNFT = false;
                            sellRecord.InternalNotes = "Transfer fees";
                            //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                            //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                            flattenRecords.Add(sellRecord);
                        }

                        break;
                    }
                case Shared.Enums.TransactionEventType.sell:
                    {


                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoUserTransaction();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn ?? 0m;
                        sellRecord.AmountAssetType = record.CurrencyIn?.ToLower();
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                        sellRecord.TransactionType = Shared.Enums.TransactionEventType.sell;
                        sellRecord.IsNFT = false;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue =  (useProvidedExchangeRate? record.ExchangeRate:0d);

                        flattenRecords.Add(sellRecord);

                        var buyRecord = new CryptoUserTransaction();
                        buyRecord.TaxableEvent = false;
                        buyRecord.Amount = record.AmountOut ?? 0m;
                        buyRecord.AmountAssetType = record.CurrencyOut?.ToLower();
                        buyRecord.Sequence = record.Sequence;
                        buyRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                        buyRecord.TransactionType = Shared.Enums.TransactionEventType.buy;
                        buyRecord.IsNFT = false;
                        //buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        flattenRecords.Add(buyRecord);
                        break;
                    }
                case Shared.Enums.TransactionEventType.nftsell:
                    {
                        //we need to apply some logic in case an exchange rate has been provided, in order to determine the sell value / conversion rate.
                        //bool useProvidedExchangeRate = false;
                        //if (record.ExchangeRate is not null && record.ExchangeRate > 0)
                        //{
                        //    useProvidedExchangeRate = true;
                        //}

                        //exchange rate and exchange currency in a SELL reflects the exchange rate sold in in to (Ie; the new buy exchange rate to use)
                        var sellRecord = new CryptoUserTransaction();


                        sellRecord.TaxableEvent = true;
                        sellRecord.Amount = record.AmountIn ?? 0m;
                        sellRecord.AmountAssetType = record.CurrencyIn?.ToLower();
                        sellRecord.Sequence = record.Sequence;
                        sellRecord.Value = record.AmountOut;
                        sellRecord.ValueAssetType = record.CurrencyOut?.ToLower();
                        sellRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                        sellRecord.TransactionType = Shared.Enums.TransactionEventType.nftsell;
                        sellRecord.IsNFT = true;
                        //sellRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //sellRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        flattenRecords.Add(sellRecord);

                        var buyRecord = new CryptoUserTransaction();
                        buyRecord.TaxableEvent = false;
                        buyRecord.Amount = record.AmountOut ?? 0m;
                        buyRecord.AmountAssetType = record.CurrencyOut?.ToLower();
                        buyRecord.Sequence = record.Sequence;
                        buyRecord.TransactionDate = record.TransactionDate ?? DateTime.MinValue;
                        buyRecord.TransactionType = Shared.Enums.TransactionEventType.buy;
                        buyRecord.IsNFT = true;
                        //buyRecord.ExchangeRateCurrency = record.ExchangeCurrency;
                        //buyRecord.ExchangeRateValue = (useProvidedExchangeRate ? record.ExchangeRate : 0d);

                        flattenRecords.Add(buyRecord);

                        break;
                    }
                case Shared.Enums.TransactionEventType.unstake:
                    {
                        //buy

                        flattenRecords.Add(new CryptoUserTransaction
                        {
                            TaxableEvent = false,
                            Amount = record.AmountOut ?? 0m,
                            AmountAssetType = record.CurrencyOut?.ToLower(),
                            Sequence = record.Sequence,
                            TransactionDate = record.TransactionDate ?? DateTime.MinValue,
                            TransactionType = Shared.Enums.TransactionEventType.buy,
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


            return flattenRecords;
        }

        private static InvalidCryptoUserTransaction? ValidateCryptoTransactionRecords(List<CryptoUserTransaction> transactions)
        {
            List<TransactionReferenceTrail> transactionLog = new();
            InvalidCryptoUserTransaction invalidCryptoUserTransactions = new();
            //invalidCryptoUserTransactions.TransactionsReferenceTrails = new List<TransactionReferenceTrail>(

            bool allTransactionsValid = true;  // Flag to indicate if all transactions are valid
            var TOLERANCE = 0.000000001m;  // Tolerance to handle floating-point inaccuracies

            // A dictionary to hold available amounts for each token
            Dictionary<string, decimal> tokenBalances = new Dictionary<string, decimal>();

            foreach (var transaction in transactions)
            {
                transaction.Approved = true; // auto approve all transactions

                decimal amount = transaction.TransactionType == Shared.Enums.TransactionEventType.sell
                    ? Math.Abs(transaction.Amount)
                    : transaction.Amount;

                if (transaction.TransactionType == Shared.Enums.TransactionEventType.buy)
                {
                    if (tokenBalances.ContainsKey(transaction.AmountAssetType))
                    {
                        tokenBalances[transaction.AmountAssetType] += amount;
                    }
                    else
                    {
                        tokenBalances[transaction.AmountAssetType] = amount;
                    }
                }
                else if (transaction.TransactionType == Shared.Enums.TransactionEventType.sell)
                {
                    if (!tokenBalances.ContainsKey(transaction.AmountAssetType) || tokenBalances[transaction.AmountAssetType] - amount < -TOLERANCE)
                    {
                        // Problem detected, log all transactions of this asset
                        allTransactionsValid = false;
                        string problematicAsset = transaction.AmountAssetType;

                        // Reset token balance for the problematic asset for logging purposes
                        if (tokenBalances.ContainsKey(problematicAsset))
                        {
                            tokenBalances[problematicAsset] = 0;
                        }

                        foreach (var t in transactions)
                        {
                            if (t.AmountAssetType == problematicAsset)
                            {
                                decimal availableBalanceBefore = tokenBalances.ContainsKey(t.AmountAssetType) ? tokenBalances[t.AmountAssetType] : 0;

                                // Adjust the balance after this transaction
                                decimal transactionAmount = t.TransactionType == Shared.Enums.TransactionEventType.sell
                                    ? -Math.Abs(t.Amount)
                                    : t.Amount;

                                tokenBalances[t.AmountAssetType] = availableBalanceBefore + transactionAmount;
                                decimal availableBalanceAfter = tokenBalances[t.AmountAssetType];


                                transactionLog.Add(new TransactionReferenceTrail
                                {
                                    Sequence = t.Sequence,
                                    TransactionDate = t.TransactionDate,
                                    TransactionType = t.TransactionType,
                                    Amount = t.Amount,
                                    AssetType = t.AmountAssetType,
                                    BalanceAfter = availableBalanceAfter
                                });



                            }

                            if (t == transaction) // Stop at the problematic transaction
                            {
                                break;
                            }
                        }

                        invalidCryptoUserTransactions.TransactionsReferenceTrails = transactionLog;
                        invalidCryptoUserTransactions.AssetType = problematicAsset;
                        invalidCryptoUserTransactions.AvailableBalance = tokenBalances.ContainsKey(transaction.AmountAssetType) ? tokenBalances[transaction.AmountAssetType] : 0;
                        invalidCryptoUserTransactions.TransactedBalance = amount;


                        return invalidCryptoUserTransactions;
                    }
                    tokenBalances[transaction.AmountAssetType] -= amount;
                }
            }

            // Return null if all transactions are valid
            return null;
        }

        /// <summary>
        /// Augments non NFT crypto records with corresponding exchange rates and calculates total amounts
        /// </summary>
        /// <param name="records">Crypto records</param>
        /// <param name="exchangeRates">List of exchange rates to use for reference</param>
        /// <returns>Augmented list of (non NFT) crypto transactions</returns>
        public static List<CryptoUserTransaction> AddExchangeRates(List<CryptoUserTransaction> transactions, List<ExchangeRate> exchangeRates)
        {

            List<CryptoUserTransaction> records = new List<CryptoUserTransaction>();
            records.AddRange(transactions.Where(x => x.IsNFT == false));
            if (records.Count > 0)
            {
                List<CryptoUserTransaction> result = new List<CryptoUserTransaction>();
                if (records.Count > 2)
                {
                    return records.Select(x =>
                    {
                        x.InternalNotes = "Skipped, scenario not supported.";
                        return x;
                    }).ToList();
                }

                //init
                CryptoUserTransaction updatedRecord;

                if (records.Count > 1)
                {
                    //sell & nftbuy & nftsell contain an in and out transaction; for which 
                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.sell))
                    {
                        //exchange rate for BUY will be inferred to be in line with the sell amount.
                        //Ie; if the sell is worth 100AUD the subsequent buy will be 100AUD

                        var sellRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.sell);
                        updatedRecord = new CryptoUserTransaction();
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

                        DateTime? exchangeRateDay = sellRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime? exchangeRateMaxOffset = exchangeRateDay?.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                                                                // string transactionType = sellRecord.TransactionType ?? string.Empty;
                        decimal exchangeRate = 0m;

                        //skip looking up exchange rate, if below conditions matches

                        // const int MAX_ITERATIONS = 5; // Set an arbitrary number as the maximum iteration limit
                        // int iterationCount = 0;

                        //while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        //{
                        //    //  iterationCount++;
                        //    //  Console.WriteLine($"sell Iteration {iterationCount}");
                        //    var exchangeRateInformation = sellRecord.ExchangeRateValue is null || sellRecord.ExchangeRateValue == 0 ? exchangeRates.FirstOrDefault(x => x.Symbol.ToLower() == targetCurrency.ToLower() && x.Date == exchangeRateDay) : null;

                        //    if (exchangeRateInformation is not null && !string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency))
                        //    {
                        //        targetCurrency = exchangeRateInformation.ExchangeCurrency;

                        //        if (exchangeRate == 0m)
                        //        {
                        //            exchangeRate = exchangeRateInformation.Low;
                        //        }
                        //        else
                        //        {
                        //            var previousExchangeRate = exchangeRate;
                        //            exchangeRate = previousExchangeRate * exchangeRateInformation.Low;
                        //        }

                        //        //high - low difference tolerance check
                        //    }
                        //    else
                        //    {
                        //        if (sellRecord.ExchangeRateValue == 0)
                        //        {
                        //            if (exchangeRateDay >= exchangeRateMaxOffset)
                        //            {
                        //                //attempt to lookup exchange-rate for previous day
                        //                exchangeRateDay = exchangeRateDay.AddDays(-1);
                        //                updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";

                        //            }
                        //            else
                        //            {
                        //                updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                        //                break; //exit while;
                        //            }


                        //        }
                        //        else
                        //        {
                        //            updatedRecord.UsesManualAssignedExchangeRate = true;
                        //            exchangeRate = sellRecord.ExchangeRateValue ?? 0m;
                        //        }

                        //    }
                        //}

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = sellRecord.TransactionType;
                        updatedRecord.Value = sellRecord.Amount * exchangeRate;
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }

                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.buy))
                    {
                        //exchange rate for BUY will be inferred to be in line with the sell amount.
                        //Ie; if the sell is worth 100AUD the subsequent buy will be 100AUD

                        var buyRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.buy);

                        updatedRecord = new CryptoUserTransaction();

                        var buyAmountValue = result.First(x => x.TransactionType == Shared.Enums.TransactionEventType.sell).Value;
                        var buyAmountCurrency = result.First(x => x.TransactionType == Shared.Enums.TransactionEventType.sell).ExchangeRateCurrency;

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
                        updatedRecord.ExchangeRateValue = buyAmountValue / buyRecord.Amount;

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
                    updatedRecord = new CryptoUserTransaction();

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
                    DateTime? exchangeRateDay = record.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                    DateTime? exchangeRateMaxOffset = exchangeRateDay?.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day

                    string targetCurrency = sourceCurrency; // string.Empty;
                    //string transactionType = record.TransactionType ?? string.Empty;
                    decimal exchangeRate = 0m;

                    // int iterationCount = 0;
                    while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                    {
                        //  iterationCount++;
                        //  Console.WriteLine($"[buy] Iteration {iterationCount}");
                        // Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                        var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol.ToLower() == targetCurrency.ToLower() && x.Date == exchangeRateDay);
                        //var exchangeRateInformation = (record.ExchangeRateValue is null || record.ExchangeRateValue == 0 ? exchangeRates.FirstOrDefault(x => x.Symbol == targetCurrency && x.Date == exchangeRateDay) : null);


                        if (exchangeRateInformation is not null && !string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency))
                        {
                            targetCurrency = exchangeRateInformation.ExchangeCurrency;
                            double low = Convert.ToDouble(exchangeRateInformation.Low);
                            double high = Convert.ToDouble(exchangeRateInformation.High);
                            bool flagHighDailyPriceFlux = false;


                            if (exchangeRate == 0m)
                            {
                                exchangeRate = record.TransactionType == Shared.Enums.TransactionEventType.buy ? exchangeRateInformation.High : exchangeRateInformation.Low;
                            }
                            else
                            {
                                var previousExchangeRate = exchangeRate;
                                exchangeRate = previousExchangeRate * (record.TransactionType == Shared.Enums.TransactionEventType.buy ? exchangeRateInformation.High : exchangeRateInformation.Low);

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
                                    exchangeRateDay = exchangeRateDay?.AddDays(-1);
                                    updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay?.ToString("D")} instead";
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
                                exchangeRate = record.ExchangeRateValue ?? 0m;
                            }

                        }
                    }

                    //update exchange rates

                    updatedRecord.ExchangeRateValue = exchangeRate;
                    updatedRecord.ExchangeRateCurrency = targetCurrency;
                    updatedRecord.TransactionType = record.TransactionType;
                    updatedRecord.Value = record.Amount * exchangeRate;
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
        public static List<CryptoUserTransaction> AddNFTExchangeRates(List<CryptoUserTransaction> transactions, List<ExchangeRate> exchangeRates)
        {


            List<CryptoUserTransaction> records = new List<CryptoUserTransaction>();
            records.AddRange(transactions.Where(x => x.IsNFT == true));
            if (records.Count > 0)
            {
                List<CryptoUserTransaction> result = new List<CryptoUserTransaction>();
                if (records.Count > 2)
                {
                    return records.Select(x =>
                    {
                        x.InternalNotes = "Skipped, scenario not supported.";
                        return x;
                    }).ToList();
                }

                //init
                CryptoUserTransaction updatedRecord;

                if (records.Count > 1)
                {
                    //NFTBUY Logic
                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.sell))
                    {
                        updatedRecord = new CryptoUserTransaction();
                        var sellNFTRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.sell);

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
                        DateTime? exchangeRateDay = sellNFTRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime? exchangeRateMaxOffset = exchangeRateDay?.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                                                                // string transactionType = sellNFTRecord.TransactionType ?? string.Empty;
                        decimal exchangeRate = 0m;

                        //  int iterationCount = 0;
                        //while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        //{
                        //    //  iterationCount++;
                        //    //  Console.WriteLine($"nft-buy [sell] Iteration {iterationCount}");
                        //    //Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                        //    var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol.ToLower() == targetCurrency.ToLower() && x.Date == exchangeRateDay);

                        //    if (exchangeRateInformation is not null && !string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency))
                        //    {
                        //        targetCurrency = exchangeRateInformation.ExchangeCurrency;

                        //        if (exchangeRate == 0d)
                        //        {
                        //            exchangeRate = Convert.ToDouble(exchangeRateInformation.Low);
                        //        }
                        //        else
                        //        {
                        //            var previousExchangeRate = exchangeRate;
                        //            exchangeRate = previousExchangeRate * Convert.ToDouble(exchangeRateInformation.Low);

                        //        }

                        //        //high - low difference tolerance check
                        //    }
                        //    else
                        //    {
                        //        if (sellNFTRecord.ExchangeRateValue == 0)
                        //        {
                        //            if (exchangeRateDay >= exchangeRateMaxOffset)
                        //            {
                        //                //attempt to lookup exchange-rate for previous day
                        //                exchangeRateDay = exchangeRateDay.AddDays(-1);
                        //                updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                        //            }
                        //            else
                        //            {
                        //                updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                        //                break; //exit while;
                        //            }

                        //        }
                        //        else
                        //        {
                        //            updatedRecord.UsesManualAssignedExchangeRate = true;
                        //            exchangeRate = sellNFTRecord.ExchangeRateValue ?? 0d;
                        //        }

                        //    }
                        //}

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = sellNFTRecord.TransactionType;
                        updatedRecord.Value = sellNFTRecord.Amount * exchangeRate;
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }
                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.nftbuy))
                    {
                        var sellRecord = result.First();

                        var buyNFTRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.nftbuy);
                        updatedRecord = new CryptoUserTransaction();
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
                        updatedRecord.TransactionType = Shared.Enums.TransactionEventType.buy;
                        updatedRecord.ExchangeRateValue = sellRecord.Value / buyNFTRecord.Amount;
                        updatedRecord.ExchangeRateCurrency = sellRecord.ExchangeRateCurrency;

                        result.Add(updatedRecord);
                    }

                    //NFTSELL Logic
                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.buy))
                    {
                        updatedRecord = new CryptoUserTransaction();
                        var sellNFTRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.buy);

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
                        DateTime? exchangeRateDay = sellNFTRecord.TransactionDate.ToUniversalTime().Date; //using UTC to lookup
                        DateTime? exchangeRateMaxOffset = exchangeRateDay?.AddDays(-7); //used to determine how many days we can look back in case of no exchange rates being found for the given day
                        string targetCurrency = sourceCurrency; // string.Empty;
                                                                // string transactionType = sellNFTRecord.TransactionType ?? string.Empty;
                        decimal exchangeRate = 0m;

                        //  int iterationCount = 0;
                        //while (targetCurrency != string.Empty && targetCurrency.ToLower() != "aud") //we need to perform an extra lookups
                        //{
                        //    //   iterationCount++;
                        //    //   Console.WriteLine($"nft-sell [buy] Iteration {iterationCount}");
                        //    //Console.WriteLine($"look up {targetCurrency} for {exchangeRateDay}");
                        //    var exchangeRateInformation = exchangeRates.FirstOrDefault(x => x.Symbol.ToLower() == targetCurrency.ToLower() && x.Date == exchangeRateDay);

                        //    if (exchangeRateInformation is not null && !string.IsNullOrWhiteSpace(exchangeRateInformation.ExchangeCurrency))
                        //    {
                        //        targetCurrency = exchangeRateInformation.ExchangeCurrency;

                        //        if (exchangeRate == 0d)
                        //        {
                        //            exchangeRate = Convert.ToDouble(exchangeRateInformation.Low);
                        //        }
                        //        else
                        //        {
                        //            var previousExchangeRate = exchangeRate;
                        //            exchangeRate = previousExchangeRate * Convert.ToDouble(exchangeRateInformation.Low);

                        //        }

                        //        //high - low difference tolerance check
                        //    }
                        //    else
                        //    {
                        //        if (sellNFTRecord.ExchangeRateValue == 0)
                        //        {
                        //            if (exchangeRateDay >= exchangeRateMaxOffset)
                        //            {
                        //                //attempt to lookup exchange-rate for previous day
                        //                exchangeRateDay = exchangeRateDay.AddDays(-1);
                        //                updatedRecord.InternalNotes = $"No Exchange rate found for transaction date; using rate from {exchangeRateDay.ToString("D")} instead";
                        //            }
                        //            else
                        //            {
                        //                updatedRecord.InternalNotes = "Exchange rate could not be looked up; requires user intervention";
                        //                break; //exit while;
                        //            }

                        //        }
                        //        else
                        //        {
                        //            updatedRecord.UsesManualAssignedExchangeRate = true;
                        //            exchangeRate = sellNFTRecord.ExchangeRateValue ?? 0d;
                        //        }

                        //    }
                        //}

                        //update exchange rates

                        updatedRecord.ExchangeRateValue = exchangeRate;
                        updatedRecord.ExchangeRateCurrency = targetCurrency;
                        updatedRecord.TransactionType = sellNFTRecord.TransactionType;
                        updatedRecord.Value = sellNFTRecord.Amount * exchangeRate;
                        updatedRecord.ValueAssetType = targetCurrency;
                        result.Add(updatedRecord);



                    }
                    if (records.Any(x => x.TransactionType == Shared.Enums.TransactionEventType.nftsell))
                    {
                        var sellRecord = result.First();

                        var buyNFTRecord = records.First(x => x.TransactionType == Shared.Enums.TransactionEventType.nftsell);

                        updatedRecord = new CryptoUserTransaction();
                        updatedRecord.Sequence = buyNFTRecord.Sequence;
                        updatedRecord.Value = sellRecord.Value;
                        updatedRecord.ValueAssetType = buyNFTRecord.ExchangeRateCurrency ?? sellRecord.ValueAssetType;
                        updatedRecord.TransactionDate = buyNFTRecord.TransactionDate.ToUniversalTime();

                        updatedRecord.Amount = buyNFTRecord.Amount;
                        updatedRecord.AmountAssetType = buyNFTRecord.AmountAssetType;
                        updatedRecord.IsNFT = buyNFTRecord.IsNFT;
                        updatedRecord.UsesManualAssignedExchangeRate = false;
                        updatedRecord.TaxableEvent = buyNFTRecord.TaxableEvent;
                        updatedRecord.InternalNotes = buyNFTRecord.InternalNotes;
                        updatedRecord.TransactionType = Shared.Enums.TransactionEventType.sell;
                        updatedRecord.ExchangeRateValue = sellRecord.Value / buyNFTRecord.Amount;
                        updatedRecord.ExchangeRateCurrency = buyNFTRecord.ExchangeRateCurrency ?? sellRecord.ExchangeRateCurrency;

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


        public async Task<List<TaxReportSummary>> GetTaxReportSummaryOld(int taxYear = 0, decimal capitalGainTaxPercentage = 30m)
        {
            List<TaxReportDetail> taxReports = await GetTaxReportDetails();

            var summary = taxReports
                .GroupBy(r => r.TaxYear)
                .Select(g => new TaxReportSummary
                {
                    TaxYear = g.Key,
                    TaxCurrency = taxReports.First().Currency,
                    TotalSaleProceeds = g.Sum(r => r.SellPrice * r.SellAmount),
                    TotalReportableAsIncome = 0m,
                    TotalCapitalGains = g.Sum(r => r.CapitalGainAmount)
                })
                .ToList();

            if (taxYear < 0)
            {
                return summary.OrderBy(x => x.TaxYear).ToList();
            }
            else
            {

                return summary.Where(x => x.TaxYear == (taxYear == 0 ? GetAustralianTaxYear(DateTime.Now) : taxYear)).OrderBy(x => x.TaxYear).ToList();
            }


        }

        public async Task<List<TaxReportSummary>> GetTaxReportSummary(int taxYear = 0, decimal capitalGainTaxPercentage = 30m)
        {
            List<TaxReportDetail> taxReports = await GetTaxReportDetails(taxYear, capitalGainTaxPercentage);

            var tasks = taxReports
                .GroupBy(r => r.TaxYear)
                .Select(async g => new TaxReportSummary
                {
                    TaxYear = g.Key,
                    TaxCurrency = taxReports.First().Currency,
                    TotalSaleProceeds = g.Sum(r => r.SellPrice * r.SellAmount),
                    TotalReportableAsIncome = await GetReportableIncome(g.Key),
                    TotalCapitalGains = g.Sum(r => r.CapitalGainAmount)
                });

            var summary = await Task.WhenAll(tasks);

            if (taxYear < 0)
            {
                return summary.OrderBy(x => x.TaxYear).ToList();
            }
            else
            {
                return summary.Where(x => x.TaxYear == (taxYear == 0 ? GetAustralianTaxYear(DateTime.Now) : taxYear)).OrderBy(x => x.TaxYear).ToList();
            }
        }


        public async Task<List<TaxReportDetail>> GetTaxReportDetails(int taxYear = 0, decimal capitalGainTaxPercentage = 30m)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var transactions = await dbContext.CryptoUserTransactions.ToListAsync();

            //get transactions which have a 0 value for all exchange rates
            var invalidTransactions = transactions.Where(x => x.ExchangeRateValue == 0).ToList();

            //now we need to augment the transactions with these exchange rates with the nearest available data from the exchange rates



            //Get holdings
            var cryptoCollection = new List<Holding>();
            foreach (var buyRecords in transactions.Where(x => x.TransactionType == Shared.Enums.TransactionEventType.buy).OrderBy(x => x.TransactionDate))
            {
                cryptoCollection.Add(new Holding()
                {
                    CreatedOn = DateOnly.FromDateTime(buyRecords.TransactionDate),
                    Available = buyRecords.Amount,
                    Name = buyRecords.AmountAssetType,
                    BoughtAt = buyRecords?.ExchangeRateValue ?? 0m,
                    Currency = buyRecords?.ExchangeRateCurrency ?? "??"
                });
            }

            var endDate = new DateTime((taxYear == 0 ? DateTime.Now.Year : taxYear), 7, 1);
            var result = new List<TaxReportDetail>();


            //we will be leveraging High-in First-out (HIFO)
            foreach (var taxableTransactions in transactions.Where(x => x.TransactionDate < endDate && x.TaxableEvent).OrderBy(x => x.TransactionDate))
            {
                //retrieve sell exchange rate
                //var sellExchangeRate = Convert.ToDouble(exchangeRates.FirstOrDefault(x => x.Date == taxableTransactions.TransactionDate && x.Symbol?.ToLower() == taxableTransactions?.AmountAssetType?.ToLower())?.Low);

                //lookup HIFO entry
                var shortlistedCollections = cryptoCollection.Where(x => x.CreatedOn <= DateOnly.FromDateTime(taxableTransactions.TransactionDate) && x.Name.ToLower() == taxableTransactions?.AmountAssetType?.ToLower() && x.Available > 0);
                var sellAmount = taxableTransactions.Amount;
                foreach (var matchedCollections in shortlistedCollections.OrderByDescending(x => x.BoughtAt))
                {
                    if (matchedCollections.RecordedTransactions is null)
                    {
                        matchedCollections.RecordedTransactions = new();
                    }

                    //check if current item has sufficient funds
                    if (matchedCollections.Available - sellAmount >= 0)
                    {
                        //sufficient
                        DateTime endOfCurrentFinancialYear = new DateTime(2022, 6, 30);
                        //bool useRebate = (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(endOfCurrentFinancialYear));

                        TaxReportDetail TaxRecord = new TaxReportDetail();
                        TaxRecord.BoughtDate = matchedCollections.CreatedOn.ToDateTime(new TimeOnly(0, 0));
                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Asset = matchedCollections.Name;
                        TaxRecord.SellAmount = sellAmount;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.TaxYear = GetAustralianTaxYear(taxableTransactions.TransactionDate);
                       

                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0m;

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            //only apply if the sell price is less than the buy price
                            bool useRebate = TaxRecord.SellPrice > TaxRecord.BuyPrice;
                            if (useRebate)
                            {
                                TaxRecord.CapitalGainAmount = (((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * sellAmount) * 0.5m;
                                TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {sellAmount}) * 0.5 (50% taxdiscount)";
                            }
                            else
                            {
                                TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * sellAmount;
                                TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {sellAmount}) (50% taxdiscount not applicable as we have a loss)";
                            }
                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * sellAmount;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {sellAmount}";
                        }

                         
                        result.Add(TaxRecord);

                        //update collection

                        matchedCollections.Available = matchedCollections.Available - sellAmount;
                        matchedCollections.RecordedTransactions.Add(TaxRecord);
                        sellAmount = 0m;
                        //exit foreach
                        break;
                    }
                    else
                    {
                        //insufficient so max out, current
                        decimal availableAmount = matchedCollections.Available;

                        TaxReportDetail TaxRecord = new TaxReportDetail();
                        TaxRecord.BoughtDate = matchedCollections.CreatedOn.ToDateTime(new TimeOnly(0, 0));
                        TaxRecord.BuyPrice = matchedCollections.BoughtAt;
                        TaxRecord.Currency = matchedCollections.Currency;
                        TaxRecord.Asset = matchedCollections.Name;
                        TaxRecord.SellAmount = availableAmount;
                        TaxRecord.SellDate = taxableTransactions.TransactionDate;
                        TaxRecord.TaxYear = GetAustralianTaxYear(taxableTransactions.TransactionDate);
                        TaxRecord.SellPrice = taxableTransactions.ExchangeRateValue ?? 0m;
                        

                        //do we apply discount
                        if (matchedCollections.CreatedOn.AddYears(1) <= DateOnly.FromDateTime(TaxRecord.SellDate))
                        {
                            //only apply if the sell price is less than the buy price
                            bool useRebate = TaxRecord.SellPrice > TaxRecord.BuyPrice;
                            if (useRebate)
                            {
                                TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * availableAmount * 0.5m;
                                TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {availableAmount}) * 0.5 (50% taxdiscount)";
                            }
                            else
                            {
                                TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * availableAmount;
                                TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {availableAmount}) (50% taxdiscount not applicable as we have a loss)";
                            }


                        }
                        else
                        {
                            TaxRecord.CapitalGainAmount = ((taxableTransactions.ExchangeRateValue ?? 0m) - matchedCollections.BoughtAt) * availableAmount;
                            TaxRecord.Calculation = $"(({taxableTransactions.ExchangeRateValue ?? 0m}) - {matchedCollections.BoughtAt}) * {availableAmount}";
                        }

                        result.Add(TaxRecord);

                        //update sell amount remaining
                        sellAmount = sellAmount - availableAmount;

                        //update collection
                        matchedCollections.RecordedTransactions.Add(TaxRecord);
                        matchedCollections.Available = 0m;
                    }
                }


            }

            return result;


        }


        public async Task<decimal> GetReportableIncome(int taxYear)
        {
            var taxYearDates = GetTaxYearDates(taxYear);
            var fromDate = taxYearDates.start.ToDateTime(new TimeOnly(0, 0));
            var endDate = taxYearDates.end.ToDateTime(new TimeOnly(23, 59));

            //now sum ReportableIncome from CryptoUserTransactions given the date time range
            using var dbContext = _dbContextFactory.CreateDbContext();
            var transactions = await dbContext.CryptoUserTransactions.Where(x => x.TransactionDate >= fromDate && x.TransactionDate <= endDate && x.ReportableAsIncome).ToListAsync();
            var total = transactions.Sum(x => x.Value);
            return total ?? 0m;
        }

        public async Task<List<CryptoUserTransaction>> GetCryptoUserTransactions(DateTime startDate, DateTime endDate, string? asset)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            //if asset is provided, filter by asset
            if (!string.IsNullOrWhiteSpace(asset))
            {
                return await dbContext.CryptoUserTransactions.Where(x => x.TransactionDate >= startDate && x.TransactionDate <= endDate && x.AmountAssetType.ToLower() == asset.ToLower()).ToListAsync();

            }
            else
            {
                return await dbContext.CryptoUserTransactions.Where(x => x.TransactionDate >= startDate && x.TransactionDate <= endDate).ToListAsync();
            }
            //return transactions;
        }

        public async Task<string> GetCurrentHoldings(string assetName)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var transactions = await dbContext.CryptoUserTransactions.Where(x=>x.AmountAssetType == assetName.ToLower()).OrderBy(x=>x.TransactionDate).ToListAsync();

            var dataPoints = new List<GraphDataPoint>();
            decimal currentXrpAmount = 0m;
            decimal totalSpent = 0m;
            decimal totalBuys = 0m;

            var dailyData = transactions
                .GroupBy(t => t.TransactionDate.Date)
                .OrderBy(g => g.Key)
                .ToList();

            // Calculate current amount of XRP and total spent
            foreach (var entry in dailyData)
            {
                decimal dailyAmount = entry.Sum(t =>
                    t.TransactionType == Shared.Enums.TransactionEventType.buy ? t.Amount : -t.Amount);

                currentXrpAmount += dailyAmount;

                if (entry.Any(t => t.TransactionType == Shared.Enums.TransactionEventType.buy))
                {
                    totalSpent += entry.Sum(t => t.TransactionType == Shared.Enums.TransactionEventType.buy ? t.Value ?? 0 : 0);
                    totalBuys += entry.Sum(t => t.TransactionType == Shared.Enums.TransactionEventType.buy ? t.Amount : 0);
                }

                dataPoints.Add(new GraphDataPoint
                {
                    Date = entry.Key,
                    Amount = currentXrpAmount
                });
            }

            var dollarCostAverage = totalBuys == 0 ? 0 : totalSpent / totalBuys;

            // Generate the static HTML output
            return GenerateHtmlResponse(assetName, currentXrpAmount, dollarCostAverage, dataPoints);

           


        }

        public async Task<List<CryptoUserTransactionProfitLossCalculation>> CalculateProfits(string assetName)
        {

            using var dbContext = _dbContextFactory.CreateDbContext();
            var transactions = await dbContext.CryptoUserTransactions.Where(x => x.AmountAssetType == assetName.ToLower()).OrderBy(x => x.TransactionDate).ToListAsync();

            var result = new List<CryptoUserTransactionProfitLossCalculation>();

            decimal totalAmountAvailable = 0m;
            decimal totalCost = 0m;

            foreach (var transaction in transactions)
            {
                if (transaction.TransactionType == TransactionEventType.buy && transaction.Amount > 0)
                {
                    totalAmountAvailable += transaction.Amount;
                    totalCost += transaction.Amount * (transaction.Value ?? 0) / transaction.Amount;

                    if(transactions.Last() == transaction)
                    {
                        var profitCalculation = new CryptoUserTransactionProfitLossCalculation
                        {
                            SoldOn = transaction.TransactionDate,
                            TotalAmountAvailableBeforeSell = totalAmountAvailable,
                            TotalAmountAvailableAfterSell = totalAmountAvailable,
                            TotalAmountSold = 0,
                            TotalProfit = 0,
                            AverageBuyPrice = totalCost / totalAmountAvailable,
                            AmountAssetType = transaction.AmountAssetType,
                            Currency = transaction.ExchangeRateCurrency
                        };

                        result.Add(profitCalculation);
                    }
                }
                else if (transaction.TransactionType == TransactionEventType.sell)
                {
                    if (totalAmountAvailable == 0) continue;

                    decimal amountSold = transaction.Amount;
                    decimal sellValue = transaction.Value ?? 0;

                    decimal averageBuyPrice = totalCost / totalAmountAvailable;
                    decimal totalProfit = sellValue - (amountSold * averageBuyPrice);

                    var profitCalculation = new CryptoUserTransactionProfitLossCalculation
                    {
                        SoldOn = transaction.TransactionDate,
                        TotalAmountAvailableBeforeSell = totalAmountAvailable,
                        TotalAmountAvailableAfterSell = totalAmountAvailable - amountSold,
                        TotalAmountSold = amountSold,
                        TotalProfit = totalProfit,
                        AverageBuyPrice = averageBuyPrice,
                        AmountAssetType = transaction.AmountAssetType,
                        Currency = transaction.ExchangeRateCurrency
                    };

                    result.Add(profitCalculation);

                    totalAmountAvailable -= amountSold;
                    totalCost -= amountSold * averageBuyPrice;
                }
            }

            

            return result;
        }


        public async Task<CryptoUserTransactionBreakEvenCalculationResult> CalculateBreakEvenPrice(string assetName)
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var transactions = await dbContext.CryptoUserTransactions
                                              .Where(x => x.AmountAssetType == assetName.ToLower())
                                              .OrderBy(x => x.TransactionDate)
                                              .ToListAsync();

            var result = new CryptoUserTransactionBreakEvenCalculationResult();

            decimal totalAmountAvailable = 0m;
            decimal totalCost = 0m;

            foreach (var transaction in transactions)
            {
                var step = new CryptoUserTransactionBreakEvenCalculationStep
                {
                    TransactionDate = transaction.TransactionDate,
                    TransactionType = transaction.TransactionType.ToString(),
                    Amount = transaction.Amount,
                    Value = transaction.Value ?? 0m,
                    TotalAmountAvailable = totalAmountAvailable,
                    TotalCost = totalCost,
                    AverageBuyPrice = totalAmountAvailable > 0 ? totalCost / totalAmountAvailable : 0m
                };

                if (transaction.TransactionType == TransactionEventType.buy)
                {
                    totalAmountAvailable += transaction.Amount;
                    totalCost += transaction.Amount * step.Value / transaction.Amount;
                }
                else if (transaction.TransactionType == TransactionEventType.sell)
                {
                    if (totalAmountAvailable == 0) continue;

                    decimal amountSold = transaction.Amount;
                    decimal averageBuyPrice = totalCost / totalAmountAvailable;

                    totalAmountAvailable -= amountSold;
                    totalCost -= amountSold * averageBuyPrice;
                }

                step.TotalAmountAvailable = totalAmountAvailable;
                step.TotalCost = totalCost;
                step.AverageBuyPrice = totalAmountAvailable > 0 ? totalCost / totalAmountAvailable : 0m;

                result.Steps.Add(step);
            }

            result.BreakEvenPrice = totalAmountAvailable == 0 ? 0m : totalCost / totalAmountAvailable;

            return result;
        }


        private int GetAustralianTaxYear(DateTime date)
        {
            int year = date.Year;
            if (date.Month >= 7) // If the month is July (7) or later
            {
                return year + 1; // The tax year is the next year
            }
            else
            {
                return year; // The tax year is the current year
            }
        }

        private (DateOnly start, DateOnly end) GetTaxYearDates(int taxYear)
        {
            var startDate = new DateOnly(taxYear - 1, 7, 1);
            var endDate = new DateOnly(taxYear, 6, 30);
            return (startDate, endDate);
        }

        private string GenerateHtmlResponseOld(decimal currentXrpAmount, decimal dollarCostAverage, List<GraphDataPoint> dataPoints)
        {
            // Prepare data for Chart.js
            var jsonDataPoints = JsonSerializer.Serialize(dataPoints.Select(dp => new
            {
                Date = dp.Date.ToString("yyyy-MM-dd"), // Ensure date is formatted correctly for JSON
                Amount = dp.Amount
            }));

            return $@"
    <!DOCTYPE html>
    <html lang=""en"">
    <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        <link rel=""stylesheet"" href=""https://maxcdn.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css"">
        <script src=""https://cdnjs.cloudflare.com/ajax/libs/Chart.js/3.7.0/chart.min.js""></script>
        <script src=""https://cdn.jsdelivr.net/npm/dayjs@1.10.4/dayjs.min.js""></script>
        <script src=""https://cdn.jsdelivr.net/npm/chartjs-adapter-dayjs@1.1.0/dist/chartjs-adapter-dayjs.bundle.min.js""></script>
        <title>XRP Summary</title>
    </head>
    <body>
        <div class=""container"">
            <h1 class=""mt-5"">XRP Summary</h1>
            <canvas id=""xrpChart"" height=""400""></canvas>
            <div class=""mt-4"">
                <h5>Current Amount of XRP Remaining: <span id=""currentAmount"">{currentXrpAmount:F4}</span></h5>
                <h5>Dollar Cost Average for Buys: <span id=""dollarCostAvg"">{dollarCostAverage:F4}</span></h5>
            </div>
        </div>

        <script>
            // Prepare data for Chart.js
            const dataPoints = {jsonDataPoints};
            const labels = dataPoints.map(point => point.Date);
            const data = dataPoints.map(point => point.Amount);

            const ctx = document.getElementById('xrpChart').getContext('2d');
            const xrpChart = new Chart(ctx, {{
                type: 'line',
                data: {{
                    labels: labels,
                    datasets: [{{
                        label: 'Available XRP Over Time',
                        data: data,
                        fill: false,
                        borderColor: 'rgb(75, 192, 192)',
                        tension: 0.1
                    }}]
                }},
                options: {{
                    scales: {{
                        x: {{
                            type: 'time',
                            time: {{
                                unit: 'day'
                            }}
                        }},
                        y: {{
                            beginAtZero: true
                        }}
                    }}
                }}
            }});
        </script>
    </body>
    </html>";
        }

        private string GenerateHtmlResponse(string assetName, decimal amount, decimal fiatCostAverage, List<GraphDataPoint> dataPoints)
        {
            // Prepare data for Chart.js
            var jsonDataPoints = JsonSerializer.Serialize(dataPoints.Select(dp => new
            {
                Date = dp.Date.ToString("yyyy-MM-dd"), // Ensure date is formatted correctly for JSON
                Amount = dp.Amount
            }));

            return $@"
    <!DOCTYPE html>
    <html lang=""en"">
    <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        <link rel=""stylesheet"" href=""https://maxcdn.bootstrapcdn.com/bootstrap/4.5.2/css/bootstrap.min.css"">
        <script src=""https://cdnjs.cloudflare.com/ajax/libs/Chart.js/3.7.0/chart.min.js""></script>
        <script src=""https://cdn.jsdelivr.net/npm/dayjs@1.10.4/dayjs.min.js""></script>
        <script src=""https://cdn.jsdelivr.net/npm/chartjs-adapter-dayjs@1.1.0/dist/chartjs-adapter-dayjs.bundle.min.js""></script>
        <title>XRP Summary</title>
    </head>
    <body>
        <div class=""container"">
            <h1 class=""mt-5"">{assetName.ToUpper()} Summary</h1>
            <canvas id=""xrpChart"" height=""400""></canvas>
            <div class=""mt-4"">
                <h5>Current Amount of {assetName.ToUpper()} Remaining: <span id=""currentAmount"">{amount:F4}</span></h5>
                <h5>Fiat Cost Average for Buys: <span id=""dollarCostAvg"">{fiatCostAverage:F4}</span></h5>
            </div>
          
        </div>

        <script>
            // Prepare data for Chart.js
            const dataPoints = {jsonDataPoints};
            const labels = dataPoints.map(point => point.Date);
            const amounts = dataPoints.map(point => point.Amount);

            // Setup the initial data structure for Chart.js
            const data = {{
                labels: labels,
                datasets: [{{
                    label: 'Available {assetName.ToUpper()} Over Time',
                    data: amounts,
                    fill: false,  // Set to false to not fill the area under the line
                    borderColor: 'rgb(75, 192, 192)',
                    pointStyle: 'circle', // Default point style
                    pointRadius: 5,
                    pointHoverRadius: 10
                }}]
            }};

            const config = {{
                type: 'line',
                data: data,
                options: {{
                    responsive: true,
                    plugins: {{
                        title: {{
                            display: true,
                            text: '{assetName.ToUpper()} Over Time'
                        }}
                    }}
                }}
            }};

            const ctx = document.getElementById('xrpChart').getContext('2d');
            const xrpChart = new Chart(ctx, config);

             
        </script>
    </body>
    </html>";
        }
    }
}


