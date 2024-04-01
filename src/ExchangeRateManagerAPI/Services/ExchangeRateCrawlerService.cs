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
                        //check SQLite database if there are any exchange rates missing from the database given the datefrom.                        
                        DetermineExchangeRatesMissingFrom(exchangeInfo, datefrom);


                        if (exchangeInfo.ExchangeRatesMissingFrom is null)
                        {
                            continue;
                        }

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

                                        results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));

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
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,
                                            Symbol = exchangeInfo.Symbol

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

                                        results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));

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
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,

                                            Symbol = exchangeInfo.Symbol

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

                                        results.Added.Add(exchangeInfo.ExchangeName?.ToLower(), (responseFromDate, responseToDate, recordCount));

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
                                            },
                                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                            ExchangeName = exchangeInfo.ExchangeName,
                                            ExchangeSymbol = exchangeInfo.ExchangeSymbol,
                                            Kind = exchangeInfo.Kind,

                                            Symbol = exchangeInfo.Symbol

                                        });
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
                    .Where(x => exchangeRates.Any(y => y.Date == x.Date && y.Symbol == x.Symbol && y.ExchangeCurrency == x.ExchangeCurrency))
                    .ToList();

                var newExchangeRates = exchangeRates
                    .Where(x => !existingExchangeRates.Any(y => y.Date == x.Date && y.Symbol == x.Symbol && y.ExchangeCurrency == x.ExchangeCurrency))
                    .ToList();

                if (newExchangeRates is not null && newExchangeRates.Count > 0)
                {
                    await dbContext.AddRangeAsync(newExchangeRates);
                    recordsAdded = dbContext.SaveChanges();
                    
                }

            }

            return recordsAdded;


        }
    }
}
