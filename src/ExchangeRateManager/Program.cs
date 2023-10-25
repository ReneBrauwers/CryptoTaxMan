using Common.Interfaces;
using Common.Models;
using Common.Services;
using ExchangeAPI.Services;
using ExchangeRateManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;

public class Program
{
    private static IServiceProvider _serviceProvider;
    private static List<string> _supportedConversionFiats;
    private static IConfiguration _config;


    private static async Task Main(string[] args)
    {
        InitializeServices();
        using var scope = _serviceProvider.CreateScope();
        var app = scope.ServiceProvider;

        //seed
        await SeedSqlite(app);
        Console.WriteLine("Press any key to continue...");
        Console.ReadLine();


        var _coinGeckoClient = app.GetRequiredService<ICoinGecko>();
        var _yahooClient = app.GetRequiredService<IYahooFinance>();
        var _sologenicClient = app.GetRequiredService<ISologenic>();



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

        //Get unique combinations for exchangecurrecy and symbol


        //exchangeInformation = exchangeInformation.Where(x => x.ExchangeCurrency.ToLower() != "aud" && x.Symbol).ToList();


        var datefromstring = "20200101";
        string[] formats = { "yyyyMMdd" };
        DateTime.TryParseExact(datefromstring, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var datefrom);

        // List<ExchangeRate> rawResponses = new List<ExchangeRate>();
        List<ExchangeInformationRetrievalFailure> exchangeInformationRetrieveFailures = new List<ExchangeInformationRetrievalFailure>();

        var configDirectory = _config["exchangeConfigStoreLocation"];
        // var loadFromDisk = Convert.ToBoolean(_config["loadFromDisk"]);
        //  var rawResponsesOnDiskLocation = Path.Combine(configDirectory, "rawResponses.json");
        //   DirectoryInfo dirInfo = new DirectoryInfo(configDirectory);

        //if (loadFromDisk)
        //{

        // if (File.Exists(rawResponsesOnDiskLocation))
        // {
        //     rawResponses = System.Text.Json.JsonSerializer.Deserialize<List<ExchangeRate>>(File.ReadAllText(rawResponsesOnDiskLocation));
        //  }
        // }


        // if (rawResponses is null || rawResponses.Count == 0)
        // {
        //  loadFromDisk = false;
        foreach (var exchangeGroup in exchangeInformation.GroupBy(x => x.ExchangeName))
        {


            //var existingFiles = dirInfo.GetFiles("*.json", SearchOption.AllDirectories).Select(x => x.Name);
            //var excludedExchangeInfo = exchangeGroup.Where(x => existingFiles.Contains(string.Concat(x.ExchangeName.ToLower(), "_", x.ExchangeSymbol, "_", x.ExchangeCurrency,".json")));
            foreach (var exchangeInfo in exchangeGroup)//.Except(excludedExchangeInfo))
            {

                DetermineExchangeRatesMissingFrom(exchangeInfo, app, datefrom);

                
                if(exchangeInfo.ExchangeRatesMissingFrom is null)
                {                   
                    continue;
                }

                Console.WriteLine($"Getting exchange rates for {exchangeInfo.ExchangeSymbol} from {exchangeInfo.ExchangeRatesMissingFrom}");

                switch (exchangeInfo.ExchangeName?.ToLower())
                {
                    case "coingecko":
                        {
                            var response = await _coinGeckoClient.GetCryptoDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                            if (response is not null && response.Count() > 0)
                            {
                                await AddExchangeRates(app, response);
                            }
                            else
                            {
                                exchangeInformationRetrieveFailures.Add(new ExchangeInformationRetrievalFailure()
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

                            var response = await _yahooClient.GetFinancialDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                            if (response is not null && response.Count() > 0)
                            {

                                await AddExchangeRates(app, response);
                            }
                            else
                            {
                                exchangeInformationRetrieveFailures.Add(new ExchangeInformationRetrievalFailure()
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
                            var response = await _sologenicClient.GetCryptoDataRange(exchangeInfo, exchangeInfo.ExchangeRatesMissingFrom ?? datefrom);
                            if (response is not null && response.Count() > 0)
                            {

                                await AddExchangeRates(app, response);

                            }
                            else
                            {

                                //exchangeInformationRetrieveFailures.Add(datefrom,exchangeInfo);
                                exchangeInformationRetrieveFailures.Add(new ExchangeInformationRetrievalFailure()
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

        var additionalConversions = ConvertToTargetCurrency(app, "aud");
        await AddExchangeRates(app, additionalConversions);

       // foreach (var item in additionalConversions)
      //  {
      //      await AddExchangeRates(app, new List<ExchangeRate> { item });
      //  }

        ////save to disk
        //if (rawResponses?.Count > 0)
        //{
        //    rawResponses.GroupBy(x=>x.Symbol).ToList().ForEach(x =>
        //    {
        //        var fileName = string.Concat(x.Key, ".json");
        //        var filePath = Path.Combine(configDirectory,"raw", fileName);
        //        if(!Directory.Exists(Path.Combine(configDirectory, "raw")))
        //        {
        //            Directory.CreateDirectory(Path.Combine(configDirectory, "raw"));
        //        }

        //        File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(x.ToList()));
        //    });

        //}

        // }



        //var fNameRawResponsesMissing = "missing_exchangerates.json";
        //File.WriteAllText(Path.Combine(dirInfo.FullName, fNameRawResponsesMissing), System.Text.Json.JsonSerializer.Serialize(exchangeInformationRetrieveFailures));
        Console.WriteLine("Press any key to exit...");
        Console.ReadLine();
    }

    private static void InitializeServices()
    {
        var services = new ServiceCollection();

        // Setup configuration
        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
        services.AddSingleton(_config);

        services.AddLogging();


        // Add DbContext to the DI container
        services.AddDbContext<CryptoTaxManDbContext>(options =>
            options.UseSqlite(_config.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        services.AddHttpClient<IYahooFinance, YahooFinanceService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        services.AddHttpClient<ICoinGecko, CoinGeckoService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        services.AddHttpClient<ISologenic, SologenicService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        services.AddSingleton<IYahooFinance, YahooFinanceService>();
        services.AddSingleton<ICoinGecko, CoinGeckoService>();
        services.AddSingleton<ISologenic, SologenicService>();


        _serviceProvider = services.BuildServiceProvider();
        //var app = services.BuildServiceProvider();

        //var _coinGeckoClient = app.GetRequiredService<ICoinGecko>();
        //var _yahooClient = app.GetRequiredService<IYahooFinance>();
        //var _sologenicClient = app.GetRequiredService<ISologenic>();

        _supportedConversionFiats = _config["fiatSupported"].Split(',').ToList();
    }

    private static async Task SeedSqlite(IServiceProvider app)
    {
        if(!Convert.ToBoolean(_config["seedDatabase"]))
        {
            return;

        }

        if (!Directory.Exists(_config["seedDataLocation"]))
        {
            return;
        }

        var dbContext = app.GetRequiredService<CryptoTaxManDbContext>();
     
      
            // Drop the database
            dbContext.Database.EnsureDeleted();

        

        dbContext.Database.EnsureCreated();

        var baseDirectory = _config["seedDataLocation"];//Path.Combine(Environment.CurrentDirectory, "SeedData");

        //read all files ending with json in the baseDirectory and its subdirectories
        System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var seedFiles = Directory.GetFiles(baseDirectory, "*.json", SearchOption.AllDirectories);
        var counter = 0;
        foreach (var seedFile in seedFiles)
        {

            var exchangeRates = System.Text.Json.JsonSerializer.Deserialize<List<ExchangeRate>>(File.ReadAllText(seedFile), options);
            if (exchangeRates is not null && exchangeRates.Count > 0)
            {
                // Distinct check based on keys
                exchangeRates = exchangeRates
                    .GroupBy(x => new { x.Date, x.Symbol, x.ExchangeCurrency })
                    .Select(g => g.First())
                    .ToList();

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
                    dbContext.SaveChanges();
                    //Console.WriteLine($"Added {newExchangeRates.Count} exchange rates to sqlite");
                }
                else
                {
                   // Console.WriteLine($"No new exchange rates found for {exchangeRates.First().Symbol}/{exchangeRates.First().ExchangeCurrency}");
                }

                counter++;
                Console.WriteLine($"Processed {counter} of {seedFiles.Count()} files");


            }
        }

    }

    private static async Task AddExchangeRates(IServiceProvider app, List<ExchangeRate> exchangeRates)
    {
        var dbContext = app.GetRequiredService<CryptoTaxManDbContext>();


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
                dbContext.SaveChanges();
                Console.WriteLine($"Added {newExchangeRates.Count} exchange rates to db");
            }
             
        }


    }

    private static List<ExchangeRate> ConvertToTargetCurrency(IServiceProvider app, string targetCurrency = "aud")
    {
        //init
        List<string> pairs = new List<string>();
        Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
        Dictionary<string, List<string>> conversionSteps = new Dictionary<string, List<string>>();
        List<ExchangeRate> newExchangeRates = new List<ExchangeRate>();
        //Get unique symbol/exchangecurrency pairs
        var dbContext = app.GetRequiredService<CryptoTaxManDbContext>();
        var storedCurrencies = dbContext.ExchangeRates.ToList();

        dbContext.ExchangeRates.Select(x => new { x.Symbol, x.ExchangeCurrency }).Distinct().ToList().ForEach(x => pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));

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

                var startCurrency = pair.Split('/')[0];
                var convertedToCurrency = pair.Split('/')[1];
                Console.WriteLine($"Paths from {pair} to /{targetCurrency}:");
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
           
            foreach (var step in conversionStep.Value)
            {
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

        //now list all exchangerates in rawresponses where the exchange currency is not equal to targetCurrency
        var nonTargetCurrencyExchangeRates = storedCurrencies.Where(x => x.ExchangeCurrency.ToLower() != targetCurrency).ToList();

        Console.WriteLine($"{nonTargetCurrencyExchangeRates.Count} non {targetCurrency} rates remaining - Press any key to continue...");
        
        //Console.ReadLine();

        //File.WriteAllText(rawResponsesOnDiskLocation, System.Text.Json.JsonSerializer.Serialize(rawResponses));
        ////Persist AUD responses to disk
        ///

        //add storedcurrencies to database
        

        //foreach (var yearResult in storedCurrencies.Where(x => x.ExchangeCurrency.ToLower() == targetCurrency).GroupBy(x => x.Date.Year))
        //{
        //    var saveDirectory = Path.Combine(dirInfo.FullName, yearResult.Key.ToString());
        //    if (!Directory.Exists(saveDirectory))
        //    {
        //        Directory.CreateDirectory(Path.Combine(saveDirectory));
        //    }

        //    foreach (var symbolResult in yearResult.GroupBy(x => x.Symbol))
        //    {
        //        Console.WriteLine($"Extracting exchange information for {symbolResult.Key} - {yearResult.Key}");

        //        File.WriteAllText(Path.Combine(saveDirectory, $"{symbolResult.Key}.json"), System.Text.Json.JsonSerializer.Serialize(symbolResult.Select(x => x).OrderBy(x => x.Date)));
        //    }
        //}

        ////Persist NON AUD responses to disk
        //foreach (var yearResult in rawResponses.Where(x => x.ExchangeCurrency.ToLower() != "aud").GroupBy(x => x.Date.Year))
        //{
        //    var saveDirectory = Path.Combine(dirInfo.FullName, $"{yearResult.Key.ToString()}-nonaud");
        //    if (!Directory.Exists(saveDirectory))
        //    {
        //        Directory.CreateDirectory(Path.Combine(saveDirectory));
        //    }

        //    foreach (var symbolResult in yearResult.GroupBy(x => x.Symbol))
        //    {
        //        Console.WriteLine($"Extracting exchange information for {symbolResult.Key} - {yearResult.Key}");

        //        File.WriteAllText(Path.Combine(saveDirectory, $"{symbolResult.Key}.json"), System.Text.Json.JsonSerializer.Serialize(symbolResult.Select(x => x).OrderBy(x => x.Date)));
        //    }
        //}
    }

    private static void DetermineExchangeRatesMissingFrom(ExchangeInformation exchangeInfo, IServiceProvider app, DateTime defaultDateTime)
    {
        var dbContext = app.GetRequiredService<CryptoTaxManDbContext>();

        var lastDate = dbContext.ExchangeRates
            .Where(x => x.Symbol == exchangeInfo.Symbol && x.ExchangeCurrency == exchangeInfo.ExchangeCurrency)
            .Max(x => (DateTime?)x.Date);  // Cast to nullable DateTime and get Max

         
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
}