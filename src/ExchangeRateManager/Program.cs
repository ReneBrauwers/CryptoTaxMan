using Common.Interfaces;
using ExchangeAPI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Extensions.Http;
using Common.Services;
using Common.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

public class Program
{
    private static Dictionary<string, List<string>> graph = new Dictionary<string, List<string>>();
    private static Dictionary<string, List<string>> conversionSteps = new Dictionary<string, List<string>>();
    private static async Task Main(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();



        var builder = new ServiceCollection();
        builder.AddLogging();
        builder.AddSingleton(config);
        builder.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
        builder.AddHttpClient<IYahooFinance, YahooFinanceService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        builder.AddHttpClient<ICoinGecko, CoinGeckoService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        builder.AddHttpClient<ISologenic, SologenicService>().SetHandlerLifetime(TimeSpan.FromMinutes(5));//.SetHandlerLifetime(TimeSpan.FromMinutes(5)).AddPolicyHandler(GetRetryPolicy());
        builder.AddSingleton<IYahooFinance, YahooFinanceService>();
        builder.AddSingleton<ICoinGecko, CoinGeckoService>();
        builder.AddSingleton<ISologenic, SologenicService>();



        var app = builder.BuildServiceProvider();

        var _coinGeckoClient = app.GetRequiredService<ICoinGecko>();
        var _yahooClient = app.GetRequiredService<IYahooFinance>();
        var _sologenicClient = app.GetRequiredService<ISologenic>();

        List<string> supportedConversionFiats = config["fiatSupported"].Split(',').ToList();
        List<ExchangeInformation> exchangeInformation = new List<ExchangeInformation>();
        List<string> pairs = new List<string>();
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

        
        var datefromstring = "20200101";
        string[] formats = { "yyyyMMdd" };
        DateTime.TryParseExact(datefromstring, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var datefrom);

        List<ExchangeRate> rawResponses = new List<ExchangeRate>();
        List<ExchangeInformationRetrievalFailure> exchangeInformationRetrieveFailures = new List<ExchangeInformationRetrievalFailure>();

        var configDirectory = config["exchangeConfigStoreLocation"];
        var loadFromDisk = Convert.ToBoolean(config["loadFromDisk"]);
        var rawResponsesOnDiskLocation = Path.Combine(configDirectory, "rawResponses.json");
        DirectoryInfo dirInfo = new DirectoryInfo(configDirectory);

        if (loadFromDisk)
        {

            if (File.Exists(rawResponsesOnDiskLocation))
            {
                rawResponses = System.Text.Json.JsonSerializer.Deserialize<List<ExchangeRate>>(File.ReadAllText(rawResponsesOnDiskLocation));
            }
        }


        if (rawResponses is null || rawResponses.Count == 0)
        {
            loadFromDisk = false;
            foreach (var exchangeGroup in exchangeInformation.GroupBy(x => x.ExchangeName))
            {


                var existingFiles = dirInfo.GetFiles("*.json", SearchOption.AllDirectories).Select(x => x.Name);
                //var excludedExchangeInfo = exchangeGroup.Where(x => existingFiles.Contains(string.Concat(x.ExchangeName.ToLower(), "_", x.ExchangeSymbol, "_", x.ExchangeCurrency,".json")));
                foreach (var exchangeInfo in exchangeGroup)//.Except(excludedExchangeInfo))
                {

                    Console.WriteLine($"Getting exchange rates for {exchangeInfo.ExchangeSymbol}");

                    switch (exchangeInfo.ExchangeName?.ToLower())
                    {
                        case "coingecko":
                            {
                                var response = await _coinGeckoClient.GetCryptoDataRange(exchangeInfo, datefrom);
                                if (response is not null && response.Count() > 0)
                                {

                                    rawResponses.AddRange(response);
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

                                var response = await _yahooClient.GetFinancialDataRange(exchangeInfo, datefrom);
                                if (response is not null && response.Count() > 0)
                                {

                                    rawResponses.AddRange(response);
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
                                var response = await _sologenicClient.GetCryptoDataRange(exchangeInfo, datefrom);
                                if (response is not null && response.Count() > 0)
                                {

                                    rawResponses.AddRange(response);
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

            //save to disk
            if (rawResponses?.Count > 0)
            {
                rawResponses.GroupBy(x=>x.Symbol).ToList().ForEach(x =>
                {
                    var fileName = string.Concat(x.Key, ".json");
                    var filePath = Path.Combine(configDirectory,"raw", fileName);
                    if(!Directory.Exists(Path.Combine(configDirectory, "raw")))
                    {
                        Directory.CreateDirectory(Path.Combine(configDirectory, "raw"));
                    }

                    File.WriteAllText(filePath, System.Text.Json.JsonSerializer.Serialize(x.ToList()));
                });
               
            }

        }





        rawResponses.ForEach(x => pairs.Add($"{x.Symbol.ToLower()}/{x.ExchangeCurrency.ToLower()}"));

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

            // Find and print paths for each pair to /aud

            foreach (var pair in pairs)
            {

                var startCurrency = pair.Split('/')[0];
                var convertedToCurrency = pair.Split('/')[1];
                Console.WriteLine($"Paths from {pair} to /aud:");
                var paths = FindPaths(startCurrency, "aud", new List<string> { startCurrency });

                var nextCurrencyLookupSteps = paths.SelectMany(x => x).Skip(1).Where(y => y != convertedToCurrency).Select(x => $"{convertedToCurrency}/{x}").ToList();
                
                

                if (nextCurrencyLookupSteps is not null && nextCurrencyLookupSteps.Count > 0)
                {
                    conversionSteps.Add(pair, nextCurrencyLookupSteps);
                    //foreach (var path in paths)
                    //{
                    //    Console.WriteLine(string.Join(" -> ", path));
                    //}
                }

            }

        }


        //detail items which require additional conversion steps
        foreach (var conversionStep in conversionSteps.Where(x=> x.Value.Count > 0))
        {
            //csc/xrp
            Console.WriteLine($"Conversion steps required for {conversionStep.Key}");
            var baseCurrency = conversionStep.Key.Split('/')[0];
            var convertedToCurrency = conversionStep.Key.Split('/')[1];
            foreach (var step in conversionStep.Value)
            {
                Console.WriteLine($"apply conversion {step}");
                //xrp/aud
                
                rawResponses.Where(x=> x.Symbol == baseCurrency && x.ExchangeCurrency == convertedToCurrency).ToList().ForEach(additionalConversion => 
                {
                    //additionalConversion
                    //look up conversion rate for step

                    var conversionRate = rawResponses.FirstOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date == additionalConversion.Date);
                    
                    if(conversionRate is null)
                    {
                        //look up next available conversion rate for step
                        conversionRate = rawResponses.LastOrDefault(x => x.Symbol == step.Split('/')[0] && x.ExchangeCurrency == step.Split('/')[1] && x.Date <= additionalConversion.Date);
                        Console.WriteLine($"No conversion rate found for {step} on {additionalConversion.Date} so using {conversionRate.Date}");
                    }


                    if(conversionRate is not null)
                    {
                        //apply conversion rate to additionalConversion
                        additionalConversion.ExchangeCurrency = conversionRate.ExchangeCurrency;
                        additionalConversion.Close = additionalConversion.Close * conversionRate.Close;
                        additionalConversion.High = additionalConversion.High * conversionRate.High;
                        additionalConversion.Low = additionalConversion.Low * conversionRate.Low;
                        additionalConversion.Open = additionalConversion.Open * conversionRate.Open;
                        additionalConversion.LowHighAverage = additionalConversion.LowHighAverage * conversionRate.LowHighAverage;
                        additionalConversion.OpenCloseAverage = additionalConversion.OpenCloseAverage * conversionRate.OpenCloseAverage;
                        if (!string.IsNullOrWhiteSpace(additionalConversion.DataSource))
                        {
                            additionalConversion.DataSource = string.Concat(additionalConversion.DataSource, "\n", conversionRate.DataSource);
                        }
                        else
                        {
                            additionalConversion.DataSource = conversionRate.DataSource;
                        }

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



        //now list all exchangerates in rawresponses where the exchange currency is not equal to aud
        var nonAUDExchangeRates = rawResponses.Where(x => x.ExchangeCurrency.ToLower() != "aud").ToList();

        Console.WriteLine($"{nonAUDExchangeRates.Count} non AUD rates remaining - Press any key to continue...");
        Console.ReadLine();

   

      

        File.WriteAllText(rawResponsesOnDiskLocation, System.Text.Json.JsonSerializer.Serialize(rawResponses));
        //Persist AUD responses to disk
        foreach (var yearResult in rawResponses.Where(x => x.ExchangeCurrency.ToLower() == "aud").GroupBy(x => x.Date.Year))
        {
            var saveDirectory = Path.Combine(dirInfo.FullName, yearResult.Key.ToString());
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(Path.Combine(saveDirectory));
            }

            foreach (var symbolResult in yearResult.GroupBy(x => x.Symbol))
            {
                Console.WriteLine($"Extracting exchange information for {symbolResult.Key} - {yearResult.Key}");

                File.WriteAllText(Path.Combine(saveDirectory, $"{symbolResult.Key}.json"), System.Text.Json.JsonSerializer.Serialize(symbolResult.Select(x => x).OrderBy(x => x.Date)));
            }
        }

        //Persist NON AUD responses to disk
        foreach (var yearResult in rawResponses.Where(x => x.ExchangeCurrency.ToLower() != "aud").GroupBy(x => x.Date.Year))
        {
            var saveDirectory = Path.Combine(dirInfo.FullName, $"{yearResult.Key.ToString()}-nonaud");
            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(Path.Combine(saveDirectory));
            }

            foreach (var symbolResult in yearResult.GroupBy(x => x.Symbol))
            {
                Console.WriteLine($"Extracting exchange information for {symbolResult.Key} - {yearResult.Key}");

                File.WriteAllText(Path.Combine(saveDirectory, $"{symbolResult.Key}.json"), System.Text.Json.JsonSerializer.Serialize(symbolResult.Select(x => x).OrderBy(x => x.Date)));
            }
        }

        var fNameRawResponsesMissing = "missing_exchangerates.json";
        File.WriteAllText(Path.Combine(dirInfo.FullName, fNameRawResponsesMissing), System.Text.Json.JsonSerializer.Serialize(exchangeInformationRetrieveFailures));
        Console.WriteLine("Press any key to exit...");
        Console.ReadLine();
    }



    public static List<List<string>> FindPaths(string start, string end, List<string> currentPath)
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
                paths.AddRange(FindPaths(adjacent, end, newPath));
            }
        }

        return paths;
    }
}