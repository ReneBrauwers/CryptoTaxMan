using ExchangeRateManagerAPI.Controllers;
using ExchangeRateManagerAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace ExchangeRateManagerAPI.Services
{
    public class ExchangeRateCrawlerService
    {
        private readonly IDictionary<string, TaskCompletionSource<ExchangeRateSynchronisationResult>> _tasks = new Dictionary<string, TaskCompletionSource<ExchangeRateSynchronisationResult>>();
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

        public string StartNewTask(Func<Task<ExchangeRateSynchronisationResult>> taskFunc)
        {
            var taskId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<ExchangeRateSynchronisationResult>();
            _tasks.Add(taskId, tcs);

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
                }
            });

            return taskId;
        }

        public async Task<ExchangeRateSynchronisationResult> ExecuteSequentialApiCalls(string datefromstring = "20200101", string format = "yyyyMMdd")
        {
            //


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

                    foreach (var exchangeInfo in exchangeGroup)//.Except(excludedExchangeInfo))
                    {
                        
                        //check database if there are any exchange rates missing from the database given the datefrom.                        
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
                                    if (response is not null && response.Count() > 0)
                                    {
                                        //persist response into SQLite database
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added is null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        //check if key already exists, if exists then update the value
                                        if (results.Added.ContainsKey(exchangeInfo.ExchangeName?.ToLower()))
                                        {
                                            var existingValue = results.Added[exchangeInfo.ExchangeName?.ToLower()];
                                            results.Added[exchangeInfo.ExchangeName?.ToLower()] = (existingValue.from, responseToDate, existingValue.records + recordCount);
                                        }
                                        else
                                        {
                                            results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));
                                        }

                                       // results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));

                                    }
                                    else
                                    {
                                        if(results.RetrievalFailures is null)
                                        {
                                            results.RetrievalFailures = new List<ExchangeInformationRetrievalFailure>();
                                        }

                                        results.RetrievalFailures.Add(new ExchangeInformationRetrievalFailure()
                                        {
                                            retrievalDetails = new ExchangeInformationRetrievalFailureProperties()
                                            {
                                                Date = datefrom,
                                                IsDateFromQuery = true,
                                                Comments = (response is null ? "No response" : (response.Count() == 0 ? "No results returned" : ""))
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,
                                            Symbol = exchangeInfo.Symbol,
                                            ExchangeRatesMissingFrom = exchangeInfo.ExchangeRatesMissingFrom
                                            


                                        });
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
                                    if (response is not null && response.Count() > 0)
                                    {
                                        //persist response into SQLite database
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added is null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        //check if key already exists, if exists then update the value
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
                                        if (results.RetrievalFailures is null)
                                        {
                                            results.RetrievalFailures = new List<ExchangeInformationRetrievalFailure>();
                                        }

                                        results.RetrievalFailures.Add(new ExchangeInformationRetrievalFailure()
                                        {
                                            retrievalDetails = new ExchangeInformationRetrievalFailureProperties()
                                            {
                                                Date = datefrom,
                                                IsDateFromQuery = true,
                                                Comments = (response is null ? "No response" : (response.Count() == 0 ? "No results returned" : ""))
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,
                                            Symbol = exchangeInfo.Symbol,                                            
                                            ExchangeRatesMissingFrom = exchangeInfo.ExchangeRatesMissingFrom

                                        });
                                    }



                                    break;
                                }
                            case "sologenic":
                                {
                                    var response = await _sologenicService.GetCryptoDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                                    if (response is not null && response.Count() > 0)
                                    {

                                        //persist response into SQLite database
                                        var recordCount = await AddExchangeRates(response);

                                        if (results.Added is null)
                                        {
                                            results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                                        }

                                        var responseFromDate = response.Min(x => x.Date);
                                        var responseToDate = response.Max(x => x.Date);

                                        //check if key already exists, if exists then update the value
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

                                        if (results.RetrievalFailures is null)
                                        {
                                            results.RetrievalFailures = new List<ExchangeInformationRetrievalFailure>();
                                        }

                                        results.RetrievalFailures.Add(new ExchangeInformationRetrievalFailure()
                                        {
                                            retrievalDetails = new ExchangeInformationRetrievalFailureProperties()
                                            {
                                                Date = datefrom,
                                                IsDateFromQuery = true,
                                                Comments = (response is null? "No response" : (response.Count() == 0 ? "No results returned" : ""))
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,
                                            Symbol = exchangeInfo.Symbol,
                                            ExchangeRatesMissingFrom = exchangeInfo.ExchangeRatesMissingFrom
                                            

                                        });
                                    }

                                    break;
                                }
                        }
                    }
                }

                var additionalConversions = ConvertToTargetCurrency("aud");
                if (additionalConversions is not null && additionalConversions.Count > 0)
                {
                    StatusMessage = $"Performing {additionalConversions.Count} additional conversions to AUD";
                    await AddExchangeRates(additionalConversions);

                    if (results.Added is null)
                    {
                        results.Added = new Dictionary<string, (DateTime from, DateTime to, int records)>();
                    }

                    var responseFromDate = additionalConversions.Min(x => x.Date);
                    var responseToDate = additionalConversions.Max(x => x.Date);

                    //check if key already exists, if exists then update the value
                    if (results.Added.ContainsKey("manual"))
                    {
                        var existingValue = results.Added["manual"];
                        results.Added["manual"] = (existingValue.from, responseToDate, existingValue.records + additionalConversions.Count);
                    }
                    else
                    {
                        results.Added.Add("manual", (responseFromDate, responseToDate, additionalConversions.Count));
                    }
                }


                return results;
            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                throw new InvalidOperationException("An error occurred while executing the API calls.", ex);
            }
        }

    
        public (bool IsCompleted, ExchangeRateSynchronisationResult Result) CheckTaskStatus(string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var tcs))
            {
                if (tcs.Task.IsCompleted)
                {                   
                        return (true, tcs.Task.Result);
                }
            }

            return (false, null);
        }

        private void DetermineExchangeRatesMissingFrom(ExchangeInformation exchangeInfo, DateTime defaultDateTime)
        {
            try
            {
                using var dbContext = _dbContextFactory.CreateDbContext();
                var lastDate = dbContext.ExchangeRates
                    .Where(x => x.Symbol == exchangeInfo.Symbol && x.ExchangeCurrency == exchangeInfo.ExchangeCurrency)
                    .Max(x => (DateTime?)x.Date);  // Cast to nullable DateTime and get Max

                //if lastDate is null then set the date to defaultDateTime
                if (lastDate is null)
                {
                    exchangeInfo.ExchangeRatesMissingFrom = defaultDateTime;
                }
                else
                {

                    //if last date + 1 is smaller than today then we need to get the missing dates
                    if (lastDate?.AddDays(1).Date < DateTime.UtcNow.Date)
                    {
                        exchangeInfo.ExchangeRatesMissingFrom = lastDate?.AddDays(1) ?? defaultDateTime;
                    }
                    else
                    {
                        exchangeInfo.ExchangeRatesMissingFrom = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occurred while determining missing exchange rates for {exchangeInfo.ExchangeName} {exchangeInfo.Symbol} {exchangeInfo.ExchangeCurrency} {ex.Message}");
            }





        }

      


        private List<ExchangeRate> ConvertToTargetCurrency(string targetCurrency = "aud")
        {
            //init
            List<string> pairs = new List<string>();
            Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
            Dictionary<string, List<string>> conversionSteps = new Dictionary<string, List<string>>();
            List<ExchangeRate> newExchangeRates = new List<ExchangeRate>();
            //Get unique symbol/exchangecurrency pairs
            using var dbContext = _dbContextFactory.CreateDbContext();          
            var storedCurrencies = dbContext.ExchangeRates.ToList();

            dbContext.ExchangeRates.Select(x => new { x.Symbol, x.ExchangeCurrency }).Distinct().ToList().ForEach(x => pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));

            //select only those symbols which have the targetCurrency as exchangeCurrency
            var symbolsAlreadyMatchingTargetCurrency = dbContext.ExchangeRates.Where(x => x.ExchangeCurrency == targetCurrency).Select(x => x.Symbol).Distinct().ToList();
            if (symbolsAlreadyMatchingTargetCurrency is not null && symbolsAlreadyMatchingTargetCurrency.Count > 0)
            {
                //remove pairs which already have the targetCurrency as exchangeCurrency
                pairs = pairs.Where(x => !symbolsAlreadyMatchingTargetCurrency.Contains(x.Split('/')[0])).ToList();
            }

            //remove any duplicates
            pairs = pairs.Distinct().ToList();


            if (pairs is not null && pairs.Count > 0)
            {
                // Construct the graph




                foreach (var pair in pairs)
                {
                    var currencies = pair.Split('/');
                    if (!graph.ContainsKey(currencies[0]))
                    {
                        graph[currencies[0]] = new List<string>();
                    }
                    graph[currencies[0]].Add(currencies[1]);
                }

                // Find and print paths for each pair to /targetcurrency                
                foreach (var pair in pairs)
                {
                    //get position of pair in pairs and log that position
                    var currentIem =  pairs.IndexOf(pair)+1;
                    StatusMessage = $"Queueing {pair} conversion ({currentIem}/{pairs.Count})";


                    var startCurrency = pair.Split('/')[0];
                    var convertedToCurrency = pair.Split('/')[1];
                    //Console.WriteLine($"Paths from {pair} to /{targetCurrency}:");
                    var paths = FindPaths(startCurrency, targetCurrency, new List<string> { startCurrency }, graph);

                    var nextCurrencyLookupSteps = paths.SelectMany(x => x).Skip(1).Where(y => y != convertedToCurrency).Select(x => $"{convertedToCurrency}/{x}").ToList();

                    if (nextCurrencyLookupSteps is not null && nextCurrencyLookupSteps.Count > 0)
                    {
                        conversionSteps.Add(pair, nextCurrencyLookupSteps);
                    }

                }

            }

            //detail items which require additional conversion steps
            foreach (var conversionStep in conversionSteps.Where(x => x.Value.Count > 0))
            {
                //csc/xrp
                
                // Console.WriteLine($"Conversion steps required for {conversionStep.Key}");
                var baseCurrency = conversionStep.Key.Split('/')[0];
                var convertedToCurrency = conversionStep.Key.Split('/')[1];
                var steps = conversionStep.Value;
                foreach (var step in conversionStep.Value)
                {
                    var currentIem = steps.IndexOf(step) + 1;
                    StatusMessage = $"{step} conversion ({currentIem} / {steps.Count})";
                    // Console.WriteLine($"apply conversion {step}");
                    //xrp/aud

                    storedCurrencies.Where(x => x.Symbol == baseCurrency && x.ExchangeCurrency == convertedToCurrency).ToList().ForEach(additionalConversion =>
                    {
                        //additionalConversion
                        //look up conversion rate for step

                        var conversionRate = storedCurrencies.FirstOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date == additionalConversion.Date);

                        if (conversionRate is null)
                        {
                            //look up next available conversion rate for step
                            conversionRate = storedCurrencies.LastOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date <= additionalConversion.Date);
                            // Console.WriteLine($"No conversion rate found for {step} on {additionalConversion.Date} so using {conversionRate.Date}");
                        }


                        if (conversionRate is not null)
                        {
                            //apply conversion rate to additionalConversion
                            var newExchangeRate = new ExchangeRate();
                            newExchangeRate.Date = additionalConversion.Date;
                            newExchangeRate.Symbol = additionalConversion.Symbol;

                            newExchangeRate.ExchangeCurrency = conversionRate.ExchangeCurrency;
                            newExchangeRate.Close = additionalConversion.Close * conversionRate.Close;
                            newExchangeRate.High = additionalConversion.High * conversionRate.High;
                            newExchangeRate.Low = additionalConversion.Low * conversionRate.Low;
                            newExchangeRate.Open = additionalConversion.Open * conversionRate.Open;
                            newExchangeRate.LowHighAverage = additionalConversion.LowHighAverage * conversionRate.LowHighAverage;
                            newExchangeRate.OpenCloseAverage = additionalConversion.OpenCloseAverage * conversionRate.OpenCloseAverage;
                            newExchangeRate.DataSource = conversionRate.DataSource;
                            newExchangeRates.Add(newExchangeRate);

                            // Console.WriteLine($"Conversion rate applied for {step} on {additionalConversion.Date}");

                        }
                        else
                        {
                            // Console.WriteLine($"Unable to find conversion rate for {step} on {additionalConversion.Date}");
                        }
                    });
                    // pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));
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

            if (exchangeRates is not null && exchangeRates.Count > 0)
            {
                // Distinct check based on keys
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

                if (newExchangeRates is not null && newExchangeRates.Count > 0)
                {
                    //convert Symbol to lower case in newExchangeRates
                    newExchangeRates.ForEach(x => x.Symbol = x.Symbol.ToLower());

                    await dbContext.AddRangeAsync(newExchangeRates);
                    recordsAdded = dbContext.SaveChanges();
                    
                }

            }

            return recordsAdded;


        }
    }
}
