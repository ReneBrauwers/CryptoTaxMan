using Common.Extensions;
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;
using System.Web;

namespace ExchangeAPI.Services
{
    public class SologenicService : ISologenic
    {
        private readonly HttpClient _client;
        private IConfiguration _configuration { get; }

        private readonly ILogger<SologenicService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        public SologenicService(HttpClient httpClient, IConfiguration configuration, ILogger<SologenicService> logger, IAsyncPolicy<HttpResponseMessage> policy)
        {
            _configuration = configuration;
            _logger = logger;
            httpClient.BaseAddress = new Uri(_configuration["sologenicAPI"]);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
             _policy = policy;
            _client = httpClient;
        }



        public async Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate)
        {

            var toDate = DateTime.UtcNow.Date;
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var epochTo = (toDate.Date == DateTime.UtcNow.Date) ? (int)(toDate - epochStart).TotalSeconds : (int)(toDate.AddDays(1) - epochStart).TotalSeconds;
            var epochFrom = (int)(fromDate - epochStart).TotalSeconds;



            var exChangeRates = new List<ExchangeRate>();


            var extractSymbolInformation = exchangeInfo.ExchangeSymbol?.Split(new string[] { "+" }, StringSplitOptions.RemoveEmptyEntries);

            var hexEncodedSymbol = extractSymbolInformation?[0]?.ToHexString();
            var exchangeSymbol = $"{hexEncodedSymbol}+{extractSymbolInformation?[1]}";

            //var urlEncodedPart = HttpUtility.UrlEncode($"{exchangeInfo.ExchangeCurrency?.ToUpper()}/{exchangeInfo.ExchangeSymbol}");
            //var altUrlEncodedPart = HttpUtility.UrlEncode($"{exchangeInfo.ExchangeCurrency?.ToUpper()}/{exchangeSymbol}");
            var urlEncodedPart = HttpUtility.UrlEncode($"{exchangeInfo.ExchangeSymbol}/{exchangeInfo.ExchangeCurrency?.ToUpper()}");
            var altUrlEncodedPart = HttpUtility.UrlEncode($"{exchangeSymbol}/{exchangeInfo.ExchangeCurrency?.ToUpper()}");


            var uriPath = $"ohlc?symbol={urlEncodedPart}&period=1d&from={epochFrom}&to={epochTo}";
            var altUriPath = $"ohlc?symbol={altUrlEncodedPart}&period=1d&from={epochFrom}&to={epochTo}";
            string? usedUriPath;
            JsonElement toCoinJsonInfo;
            try
            {
                var result = await GetData(uriPath, altUriPath);
                usedUriPath = result.Item1;
                toCoinJsonInfo = result.Item2;
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"{ex.Message} - symbol {exchangeInfo.Symbol} exchange {exchangeInfo.ExchangeName}");
            }

            if (toCoinJsonInfo.GetArrayLength() > 0)
            {


                var lastDate = DateTime.MinValue;
                var exChangeRatesTemp = new List<ExchangeRate>();
                foreach (JsonElement price in toCoinJsonInfo.EnumerateArray())
                {
                    var epoch = price[0].GetInt64();
                    var open = Convert.ToDecimal(price[1].GetString());
                    var high = Convert.ToDecimal(price[2].GetString());
                    var low = Convert.ToDecimal(price[3].GetString());
                    var close = Convert.ToDecimal(price[4].GetString());

                    //convert epoch to datetime

                    DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, 0); //from start epoch time
                    date = date.AddSeconds(epoch); //add the seconds to the start DateTime

                    //Data granularity is automatic (cannot be adjusted)
                    //1 day from query time = 5 minute interval data
                    //1 - 90 days from query time = hourly data
                    //above 90 days from query time = daily data(00:00 UTC)
                    exChangeRatesTemp.Add(new ExchangeRate()
                    {
                        Date = date.Date,
                        Open = open,
                        Close = close,
                        High = high,
                        Low = low,
                        ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                        Symbol = exchangeInfo.Symbol,
                        DataSource = $"{_client.BaseAddress}{uriPath}",
                    });


                }

                var groupedResult = from e in exChangeRatesTemp
                                    group e by e.Date;

                //cleanup exchangeRates / group by date and average
                exChangeRates = exChangeRatesTemp.GroupBy(x => x.Date).Select(x =>
                {
                    //Logic used to derive the average
                    var openValue = exChangeRatesTemp.Where(y => y.Date == x.Key).First().Open;
                    var closeValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Last().Close;
                    var lowValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Min(r => r.Low);
                    var highValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Max(r => r.High);

                    return new ExchangeRate()
                    {
                        Date = x.Key,
                        Open = openValue,
                        Close = closeValue,
                        High = highValue,
                        Low = lowValue,
                        ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                        Symbol = exchangeInfo.Symbol,
                        DataSource = $"{_client.BaseAddress}{usedUriPath}"

                    };

                }).ToList();

                //Update close with previous value
                exChangeRates.ForEach(x =>
                {

                    var yesterday = x.Date.AddDays(-1);
                    //get yesterdays value which is todays open
                    try
                    {
                        var open = exChangeRates.Where(x => x.Date == yesterday).Select(x => x.Close).FirstOrDefault();
                        x.Open = (open > 0 ? open : x.Close);
                        x.OpenCloseAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.Open), Convert.ToDecimal(x.Close)), 2);
                        x.LowHighAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.Low), Convert.ToDecimal(x.High)), 2);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(ex.Message);
                    }

                });
            }

            return exChangeRates;





        }

        private async Task<Tuple<string, JsonElement>> GetData(string uriPath, string altUriPath)
        {
            var usedPath = uriPath;
            var coinInfo = await _policy.ExecuteAsync(() => _client.GetAsync(uriPath));
            //var coinInfo = await _client.GetAsync(uriPath);

            if (coinInfo.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
            }


            var toCoinJsonInfo = JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement;

            if ((!string.IsNullOrEmpty(altUriPath)) && toCoinJsonInfo.GetArrayLength() == 0)
            {
                coinInfo = await _policy.ExecuteAsync(() => _client.GetAsync(altUriPath));
                usedPath = altUriPath;

                if (coinInfo.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                }

                return new Tuple<string, JsonElement>(usedPath, JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement);

            }
            else
            {
                return new Tuple<string, JsonElement>(usedPath, toCoinJsonInfo);
            }





        }

    }
}
