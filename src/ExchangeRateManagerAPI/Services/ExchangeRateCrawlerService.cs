using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace ExchangeRateManagerAPI.Services
{
    public class ExchangeRateCrawlerService
    {
        private readonly IDictionary<string, TaskCompletionSource<ExchangeRateSynchronisationResult>> _tasksExchangeRateSynchronisation = new Dictionary<string, TaskCompletionSource<ExchangeRateSynchronisationResult>>();
        private readonly IDictionary<string, TaskCompletionSource<List<ExchangeRate>>> _tasksExchangeRateConversiom = new Dictionary<string, TaskCompletionSource<List<ExchangeRate>>>();
        private readonly IDictionary<string, Exception> _taskExceptions = new Dictionary<string, Exception>();
        private readonly ILogger<ExchangeRateCrawlerService> _logger;
        private readonly ISologenic _sologenicService;
        private readonly IYahooFinance _yahooFinanceService;
        private readonly ICoinGecko _coinGeckoService;
        private readonly IDbContextFactory<CryptoTaxManDbContext> _dbContextFactory;
        public string StatusMessage = string.Empty;

        public ExchangeRateCrawlerService(ILogger<ExchangeRateCrawlerService> logger, ISologenic sologenic, IYahooFinance yahooFinance, ICoinGecko coinGecko, IDbContextFactory<CryptoTaxManDbContext> dbContextFactory)
        {
            _logger = logger;
            _sologenicService = sologenic;
            _yahooFinanceService = yahooFinance;
            _coinGeckoService = coinGecko;
            _dbContextFactory = dbContextFactory;
        }

        public string StartExchangeRateSynchronisationTask(Func<Task<ExchangeRateSynchronisationResult>> taskFunc)
        {
            var taskId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ExchangeRateSynchronisationResult>();
            _tasksExchangeRateSynchronisation.Add(taskId, tcs);

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

        public string StartCurrencyConversionTask(string targetCurrency = "aud")
        {
            var taskId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<List<ExchangeRate>>();
            _tasksExchangeRateConversiom.Add(taskId, tcs);

            Task.Run(async () =>
            {
                try
                {
                    var result = await Task.Run(() =>
                    {
                        var conversionResults = new List<ExchangeRate>();
                        try
                        {

                            //Get conversion paths
                            conversionResults.AddRange(ConvertCurrencies(targetCurrency));

                            //GetCurrencyConversionPaths(targetCurrency);

                            //var convertedRates = ConvertToTargetCurrency(targetCurrency);
                            //if (convertedRates.Any())
                            //{
                            //    conversionResults = convertedRates.Select(rate => new ExchangeRate
                            //    {
                            //        Date = rate.Date,
                            //        Symbol = rate.Symbol,
                            //        ExchangeCurrency = rate.ExchangeCurrency,
                            //        Close = rate.Close,
                            //        High = rate.High,
                            //        Low = rate.Low,
                            //        Open = rate.Open,
                            //        LowHighAverage = rate.LowHighAverage,
                            //        OpenCloseAverage = rate.OpenCloseAverage,
                            //        DataSource = rate.DataSource
                            //    }).ToList();
                            //}
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error during currency conversion: {ex.Message}");
                            throw;
                        }
                        return conversionResults;
                    });

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

        public (bool IsCompleted, List<ExchangeRate> Result, Exception Error) CheckCurrencyConversionTaskStatus(string taskId)
        {
            if (_tasksExchangeRateConversiom.TryGetValue(taskId, out var tcs))
            {
                if (tcs.Task.IsCompleted)
                {
                    _taskExceptions.TryGetValue(taskId, out var exception);
                    return (true, tcs.Task.Result, exception);
                }
            }

            return (false, null, null);
        }

        public async Task<ExchangeRateSynchronisationResult> ExecuteSequentialApiCalls(string datefromstring = "20200101", string format = "yyyyMMdd")
        {
            string[] formats = { format };
            DateTime.TryParseExact(datefromstring, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var datefrom);

            List<ExchangeInformation> exchangeInformation = new List<ExchangeInformation>();

            foreach (var cryptoConfigFile in Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Assets"), "*.json"))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(cryptoConfigFile)))
                {
                    var result = await System.Text.Json.JsonSerializer.DeserializeAsync<List<ExchangeInformation>>(ms);
                    if (result is not null && result.Count > 0)
                    {
                        exchangeInformation.AddRange(result);
                    }
                }
            }

            var results = new ExchangeRateSynchronisationResult();

            try
            {
                foreach (var exchangeGroup in exchangeInformation.GroupBy(x => x.ExchangeName))
                {
                    foreach (var exchangeInfo in exchangeGroup)
                    {
                        DetermineExchangeRatesMissingFrom(exchangeInfo, datefrom);

                        if (exchangeInfo.ExchangeRatesMissingFrom is null)
                        {
                            continue;
                        }

                        StatusMessage = $"Retrieving {exchangeInfo.ExchangeName} rates from {exchangeInfo.ExchangeRatesMissingFrom?.ToString("yyyy-dd-MM")}";
                        switch (exchangeInfo.ExchangeName?.ToLower())
                        {
                            case "coingecko":
                                {
                                    var response = await _coinGeckoService.GetCryptoDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                                    if (response is not null && response.Any())
                                    {
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added == null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        if (results.Added.ContainsKey(exchangeInfo.ExchangeName?.ToLower()))
                                        {
                                            var existingValue = results.Added[exchangeInfo.ExchangeName?.ToLower()];
                                            results.Added[exchangeInfo.ExchangeName?.ToLower()] = (existingValue.from, responseToDate, existingValue.records + recordCount);
                                        }
                                        else
                                        {
                                            results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));
                                        }
                                    }
                                    else
                                    {
                                        AddRetrievalFailure(results, exchangeInfo, datefrom, response);
                                    }
                                    break;
                                }
                            case "yahoofinance":
                                {
                                    if (exchangeInfo.Kind == "stock")
                                    {
                                        datefrom = new DateTime(2015, 1, 1);
                                    }

                                    var response = await _yahooFinanceService.GetFinancialDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                                    if (response is not null && response.Any())
                                    {
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added == null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        if (results.Added.ContainsKey(exchangeInfo.ExchangeName?.ToLower()))
                                        {
                                            var existingValue = results.Added[exchangeInfo.ExchangeName?.ToLower()];
                                            results.Added[exchangeInfo.ExchangeName?.ToLower()] = (existingValue.from, responseToDate, existingValue.records + recordCount);
                                        }
                                        else
                                        {
                                            results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));
                                        }
                                    }
                                    else
                                    {
                                        AddRetrievalFailure(results, exchangeInfo, datefrom, response);
                                    }
                                    break;
                                }
                            case "sologenic":
                                {
                                    var response = await _sologenicService.GetCryptoDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                                    if (response is not null && response.Any())
                                    {
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added == null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        if (results.Added.ContainsKey(exchangeInfo.ExchangeName?.ToLower()))
                                        {
                                            var existingValue = results.Added[exchangeInfo.ExchangeName?.ToLower()];
                                            results.Added[exchangeInfo.ExchangeName?.ToLower()] = (existingValue.from, responseToDate, existingValue.records + recordCount);
                                        }
                                        else
                                        {
                                            results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));
                                        }
                                    }
                                    else
                                    {
                                        AddRetrievalFailure(results, exchangeInfo, datefrom, response);
                                    }
                                    break;
                                }
                        }
                    }
                }
               

                return results;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while executing the API calls.", ex);
            }
        }

        private void AddRetrievalFailure(ExchangeRateSynchronisationResult results, ExchangeInformation exchangeInfo, DateTime datefrom, IEnumerable<ExchangeRate> response)
        {
            if (results.RetrievalFailures == null)
            {
                results.RetrievalFailures = new List<ExchangeInformationRetrievalFailure>();
            }

            results.RetrievalFailures.Add(new ExchangeInformationRetrievalFailure()
            {
                retrievalDetails = new ExchangeInformationRetrievalFailureProperties()
                {
                    Date = datefrom,
                    IsDateFromQuery = true,
                    Comments = response == null ? "No response" : (response.Any() == false ? "No results returned" : "")
                },
                ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                ExchangeName = exchangeInfo.ExchangeName,
                ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                Kind = exchangeInfo.Kind,
                Symbol = exchangeInfo.Symbol,
                ExchangeRatesMissingFrom = exchangeInfo.ExchangeRatesMissingFrom
            });
        }

        public (bool IsCompleted, ExchangeRateSynchronisationResult Result, Exception Error) CheckExchangeRateSynchronisationTaskStatus(string taskId)
        {
            if (_tasksExchangeRateSynchronisation.TryGetValue(taskId, out var tcs))
            {
                if (tcs.Task.IsCompleted)
                {
                    _taskExceptions.TryGetValue(taskId, out var exception);
                    return (true, tcs.Task.Result, exception);
                }
            }

            return (false, null, null);
        }

        private void DetermineExchangeRatesMissingFrom(ExchangeInformation exchangeInfo, DateTime defaultDateTime)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var lastDate = dbContext.ExchangeRates
                    .Where(x => x.Symbol == exchangeInfo.Symbol && x.ExchangeCurrency == exchangeInfo.ExchangeCurrency)
                    .Max(x => (DateTime?)x.Date);

                if (lastDate is null)
                {
                    exchangeInfo.ExchangeRatesMissingFrom = defaultDateTime;
                }
                else if (lastDate?.AddDays(1).Date < DateTime.UtcNow.Date)
                {
                    exchangeInfo.ExchangeRatesMissingFrom = lastDate?.AddDays(1) ?? defaultDateTime;
                }
                else
                {
                    exchangeInfo.ExchangeRatesMissingFrom = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occurred while determining missing exchange rates for {exchangeInfo.ExchangeName} {exchangeInfo.Symbol} {exchangeInfo.ExchangeCurrency}: {ex.Message}");
            }
        }



        public List<ExchangeRate> ConvertCurrencies(string targetCurrency = "aud")
        {
            using var dbContext = _dbContextFactory.CreateDbContext();
            var conversionPaths = GetCurrencyConversionPaths(targetCurrency,dbContext);
            

            //using var dbContext = _exchangeRateService.GetDbContext();

            foreach (var path in conversionPaths)
            {
                var sourceCurrency = path.First();
                targetCurrency = path.Last();

                var recordsToConvert = dbContext.ExchangeRates
                    .Where(x => x.Symbol.ToLower() == sourceCurrency && x.ExchangeCurrency.ToLower() != targetCurrency)
                    .ToList();

                foreach (var record in recordsToConvert)
                {
                    var conversionRate = GetConversionRateForPath(path, record.Date,1, dbContext);

                    if (conversionRate is not null && targetCurrency == conversionRate.ExchangeCurrency)
                    {

                        //create new record with converted values
                        ExchangeRate ConvertedRecord = new ExchangeRate();
                        ConvertedRecord.Date = record.Date;
                        ConvertedRecord.Symbol = record.Symbol;
                        ConvertedRecord.ExchangeCurrency = conversionRate.ExchangeCurrency;
                        ConvertedRecord.Close = record.Close * conversionRate.Close;
                        ConvertedRecord.High = record.High * conversionRate.High;
                        ConvertedRecord.Low = record.Low * conversionRate.Low;
                        ConvertedRecord.Open = record.Open * conversionRate.Open;
                        ConvertedRecord.LowHighAverage = record.LowHighAverage * conversionRate.LowHighAverage;
                        ConvertedRecord.OpenCloseAverage = record.OpenCloseAverage * conversionRate.OpenCloseAverage;
                        ConvertedRecord.DataSource = $"Auto conversion to {targetCurrency}";
                        
                        dbContext.ExchangeRates.Add(ConvertedRecord);
                    }
                }
            }

            //now get all modified records and project them in a new ExchangeRate List which we return
            var modifiedRecords = dbContext.ChangeTracker.Entries<ExchangeRate>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();

            dbContext.SaveChanges();

            return modifiedRecords;
        }



        public List<List<string>> GetCurrencyConversionPaths(string targetCurrency, CryptoTaxManDbContext dbContext)
        {
            List<string> pairs = new List<string>();

            // Fetch exchange rates from database
            //using var dbContext = _dbContextFactory.CreateDbContext();

            dbContext.ExchangeRates
                .Select(x => new { x.Symbol, x.ExchangeCurrency })
                .Distinct()
                .ToList()
                .ForEach(x => pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));

            var symbolsAlreadyMatchingTargetCurrency = dbContext.ExchangeRates
                .Where(x => x.ExchangeCurrency.ToLower() == targetCurrency)
                .Select(x => x.Symbol.ToLower())
                .Distinct()
                .ToList();

            if (symbolsAlreadyMatchingTargetCurrency is not null && symbolsAlreadyMatchingTargetCurrency.Count > 0)
            {
                pairs = pairs.Where(x => !symbolsAlreadyMatchingTargetCurrency.Contains(x.Split('/')[0])).ToList();
            }

            pairs = pairs.Distinct().ToList();

            var conversionPaths = new List<List<string>>();

            foreach (var pair in pairs)
            {
                var sourceCurrency = pair.Split('/')[0];
                var path = FindConversionPath(sourceCurrency, targetCurrency, dbContext);
                if (path != null)
                {
                    conversionPaths.Add(path);
                }
            }

            return conversionPaths;
        }

        private List<string> FindConversionPath(string fromCurrency, string toCurrency, CryptoTaxManDbContext dbContext)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<List<string>>();
            queue.Enqueue(new List<string> { fromCurrency });


            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var lastCurrency = path.Last();

                if (lastCurrency == toCurrency)
                {
                    return path;
                }

                if (!visited.Contains(lastCurrency))
                {
                    visited.Add(lastCurrency);

                    var nextSteps = dbContext.ExchangeRates
                        .Where(rate => rate.Symbol.ToLower() == lastCurrency)
                        .Select(rate => rate.ExchangeCurrency.ToLower())
                        .Distinct()
                        .ToList();

                    foreach (var next in nextSteps)
                    {
                        var newPath = new List<string>(path) { next };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return null;
        }

        private ExchangeRate? GetConversionRateForPath(List<string> path, DateTime date, int maxDaysDifference, CryptoTaxManDbContext dbContext)
        {
            ExchangeRate conversionRate = new ExchangeRate();


            for (int i = 0; i < path.Count - 1; i++)
            {
                var fromCurrency = path[i];
                var toCurrency = path[i + 1];

                var rate = dbContext.ExchangeRates
                    .Where(x => x.Symbol.ToLower() == fromCurrency && x.ExchangeCurrency.ToLower() == toCurrency && x.Date <= date)
                    .OrderByDescending(x => x.Date)
                    .FirstOrDefault();

                if (rate == null || (date - rate.Date).TotalDays > maxDaysDifference)
                {
                    return null;
                }


                conversionRate.Date = rate.Date;
                conversionRate.Symbol = rate.Symbol;
                conversionRate.ExchangeCurrency = rate.ExchangeCurrency;
                conversionRate.Close = rate.Close;
                conversionRate.High = rate.High;
                conversionRate.Low = rate.Low;
                conversionRate.Open = rate.Open;
                conversionRate.LowHighAverage = rate.LowHighAverage;
                conversionRate.OpenCloseAverage = rate.OpenCloseAverage;
                conversionRate.DataSource = rate.DataSource;             

            }

            return conversionRate;
        }






        private List<ExchangeRate> ConvertToTargetCurrencyOld(string targetCurrency = "aud")
        {
            List<string> pairs = new List<string>();
            Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> conversionSteps = new Dictionary<string, List<string>>();
            List<ExchangeRate> newExchangeRates = new List<ExchangeRate>();

            using var dbContext = _dbContextFactory.CreateDbContext();
            var storedCurrencies = dbContext.ExchangeRates.ToList();

            dbContext.ExchangeRates.Select(x => new { x.Symbol, x.ExchangeCurrency }).Distinct().ToList().ForEach(x => pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));

            var symbolsAlreadyMatchingTargetCurrency = dbContext.ExchangeRates.Where(x => x.ExchangeCurrency == targetCurrency).Select(x => x.Symbol).Distinct().ToList();
            if (symbolsAlreadyMatchingTargetCurrency is not null && symbolsAlreadyMatchingTargetCurrency.Count > 0)
            {
                pairs = pairs.Where(x => !symbolsAlreadyMatchingTargetCurrency.Contains(x.Split('/')[0])).ToList();
            }

            pairs = pairs.Distinct().ToList();

            if (pairs is not null && pairs.Count > 0)
            {
                foreach (var pair in pairs)
                {
                    var currencies = pair.Split('/');
                    if (!graph.ContainsKey(currencies[0]))
                    {
                        graph[currencies[0]] = new List<string>();
                    }
                    graph[currencies[0]].Add(currencies[1]);
                }

                foreach (var pair in pairs)
                {
                    var currentIem = pairs.IndexOf(pair) + 1;
                    StatusMessage = $"Queueing {pair} conversion ({currentIem}/{pairs.Count})";

                    var startCurrency = pair.Split('/')[0];
                    var convertedToCurrency = pair.Split('/')[1];
                    var paths = FindPaths(startCurrency, targetCurrency, new List<string> { startCurrency }, graph);

                    var nextCurrencyLookupSteps = paths.SelectMany(x => x).Skip(1).Where(y => y != convertedToCurrency).Select(x => $"{convertedToCurrency}/{x}").ToList();

                    if (nextCurrencyLookupSteps is not null && nextCurrencyLookupSteps.Count > 0)
                    {
                        conversionSteps.Add(pair, nextCurrencyLookupSteps);
                    }
                }
            }

            foreach (var conversionStep in conversionSteps.Where(x => x.Value.Count > 0))
            {
                var baseCurrency = conversionStep.Key.Split('/')[0];
                var convertedToCurrency = conversionStep.Key.Split('/')[1];
                var steps = conversionStep.Value;
                foreach (var step in conversionStep.Value)
                {
                    var currentIem = steps.IndexOf(step) + 1;
                    StatusMessage = $"{step} conversion ({currentIem} / {steps.Count})";

                    storedCurrencies.Where(x => x.Symbol == baseCurrency && x.ExchangeCurrency == convertedToCurrency).ToList().ForEach(additionalConversion =>
                    {
                        var conversionRate = storedCurrencies.FirstOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date == additionalConversion.Date);
                        if (conversionRate is null)
                        {
                            conversionRate = storedCurrencies.LastOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date <= additionalConversion.Date);
                        }

                        if (conversionRate is not null)
                        {
                            var newExchangeRate = new ExchangeRate
                            {
                                Date = additionalConversion.Date,
                                Symbol = additionalConversion.Symbol,
                                ExchangeCurrency = conversionRate.ExchangeCurrency,
                                Close = additionalConversion.Close * conversionRate.Close,
                                High = additionalConversion.High * conversionRate.High,
                                Low = additionalConversion.Low * conversionRate.Low,
                                Open = additionalConversion.Open * conversionRate.Open,
                                LowHighAverage = additionalConversion.LowHighAverage * conversionRate.LowHighAverage,
                                OpenCloseAverage = additionalConversion.OpenCloseAverage * conversionRate.OpenCloseAverage,
                                DataSource = conversionRate.DataSource
                            };
                            newExchangeRates.Add(newExchangeRate);
                        }
                    });
                }
            }

            return newExchangeRates;
        }

     
        public static List<List<string>> FindPaths(string start, string end, List<string> currentPath, Dictionary<string, List<string>> graph)
        {
            List<List<string>> paths = new List<List<string>>();

            if (!graph.ContainsKey(start))
                return paths;

            foreach (var adjacent in graph[start])
            {
                if (adjacent == end)
                {
                    var newPath = new List<string>(currentPath);
                    newPath.Add(adjacent);
                    paths.Add(newPath);
                }
                else if (!currentPath.Contains(adjacent))
                {
                    var newPath = new List<string>(currentPath);
                    newPath.Add(adjacent);
                    paths.AddRange(FindPaths(adjacent, end, newPath, graph));
                }
            }

            return paths;
        }

        private async Task<int> AddExchangeRates(List<ExchangeRate> exchangeRates)
        {
            int recordsAdded = 0;

            System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            if (exchangeRates is not null && exchangeRates.Any())
            {
                exchangeRates = exchangeRates
                    .GroupBy(x => new { x.Date, x.Symbol, x.ExchangeCurrency })
                    .Select(g => g.First())
                    .ToList();

                using var dbContext = _dbContextFactory.CreateDbContext();
                var allExchangeRates = dbContext.ExchangeRates.ToList();

                var existingExchangeRates = allExchangeRates
                    .Where(x => exchangeRates.Any(y => y.Date == x.Date && y.Symbol.ToLower() == x.Symbol.ToLower() && y.ExchangeCurrency == x.ExchangeCurrency))
                    .ToList();

                var newExchangeRates = exchangeRates
                    .Where(x => !existingExchangeRates.Any(y => y.Date == x.Date && y.Symbol.ToLower() == x.Symbol.ToLower() && y.ExchangeCurrency == x.ExchangeCurrency))
                    .ToList();

                if (newExchangeRates is not null && newExchangeRates.Any())
                {
                    newExchangeRates.ForEach(x => x.Symbol = x.Symbol.ToLower());
                    await dbContext.AddRangeAsync(newExchangeRates);
                    recordsAdded = dbContext.SaveChanges();
                }
            }

            return recordsAdded;
        }
    }
}
