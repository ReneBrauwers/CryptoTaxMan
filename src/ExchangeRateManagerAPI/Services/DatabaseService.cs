using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Polly;
using Shared.Models;
using System;
using System.Text;

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



                    //var foundExchangeRate =  dbContext.ExchangeRates.Find(transaction.TransactionDate, transaction.AmountAssetType, baseExchangeCurrency);

                    // var exchangeRate = await FetchExchangeRateInformation(transaction.TransactionDate ?? DateTime.Now, transaction.AmountAssetType, baseExchangeCurrency);
                    if (exchangeRate is not null)
                    {
                        //if transaction type is a buy then use the high rate, else use the low rate
                        if (transaction.TransactionType == Shared.Enums.TransactionEventType.buy)
                        {
                            transaction.ExchangeRateValue = exchangeRate.High;
                            transaction.ExchangeRateCurrency = exchangeRate.ExchangeCurrency;
                            transaction.Value = transaction.Amount * exchangeRate.High;
                        }
                        else
                        {
                            transaction.ExchangeRateValue = exchangeRate.Low;
                            transaction.ExchangeRateCurrency = exchangeRate.ExchangeCurrency;
                            transaction.Value = transaction.Amount * exchangeRate.Low;
                        }



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
                    LastExchangeRateEntryDate = null
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
                            sellRecord.Amount = record.AmountIn ?? 0m - record.AmountOut ?? 0m;
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


        public async Task<List<TaxReportSummary>> GetTaxReportSummary(int taxYear=0, decimal capitalGainTaxPercentage = 30m )
        {
            List<TaxReportDetail> taxReports = await GetTaxReportDetails();

            var summary = taxReports
                .GroupBy(r => r.TaxYear )
                .Select(g => new TaxReportSummary
                {
                    TaxYear = g.Key,
                    TaxCurrency = taxReports.First().Currency,
                    TotalSaleProceeds = g.Sum(r => r.SellPrice * r.SellAmount),
                    TotalCapitalGains = g.Sum(r => r.CapitalGainAmount)
                })
                .ToList();

            if(taxYear < 0)
            {
                return summary.OrderBy(x => x.TaxYear).ToList();
            }
            else
            {
                
                return summary.Where(x => x.TaxYear == (taxYear==0?GetAustralianTaxYear(DateTime.Now):taxYear)).OrderBy(x=>x.TaxYear).ToList();
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

            var endDate = new DateTime((taxYear==0?DateTime.Now.Year:taxYear), 7, 1);
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


    }
}
