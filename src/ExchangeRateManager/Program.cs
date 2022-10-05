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

IConfiguration config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();



var builder = new ServiceCollection();
builder.AddLogging();
builder.AddSingleton<IConfiguration>(config);
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

Console.WriteLine("Started");
List<ExchangeInformation> exchangeInformation = new List<ExchangeInformation>();

foreach (var cryptoConfigFile in Directory.GetFiles(Path.Combine(Environment.CurrentDirectory,"Assets"),"*.json"))
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
List<ExchangeInformation> exchangeInformationRetrieveFailures = new List<ExchangeInformation>();

var configDirectory = config["exchangeConfigStoreLocation"];
DirectoryInfo dirInfo = new DirectoryInfo(configDirectory);

foreach (var exchangeGroup in exchangeInformation.GroupBy(x=>x.ExchangeName))
{


    var existingFiles = dirInfo.GetFiles("*.json",SearchOption.AllDirectories).Select(x => x.Name);
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
                        exchangeInformationRetrieveFailures.Add(exchangeInfo);
                    }
                    
                    
                    break;
                }
            case "yahoofinance":
                {
                    if(exchangeInfo.Kind == "stock")
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
                        exchangeInformationRetrieveFailures.Add(exchangeInfo);
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
                        exchangeInformationRetrieveFailures.Add(exchangeInfo);
                    }
                    
                    break;
                }
        }

       
    }
}


//Persist responses to disk
foreach (var yearResult in rawResponses.GroupBy(x => x.Date.Year))
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

var fNameRawResponsesMissing = "missing_exchangerates.json";
File.WriteAllText(Path.Combine(dirInfo.FullName, fNameRawResponsesMissing), System.Text.Json.JsonSerializer.Serialize(exchangeInformationRetrieveFailures));
Console.WriteLine("Press any key to exit...");
Console.ReadLine();

