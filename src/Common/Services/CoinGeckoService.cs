
using Common.Interfaces;
using Common.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace Common.Services
{
    public class CoinGeckoService : ICoinGecko
    {
        private readonly HttpClient _client;
        private IConfiguration _configuration { get; }

        private readonly ILogger<CoinGeckoService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public CoinGeckoService(HttpClient httpClient, IConfiguration configuration, ILogger<CoinGeckoService> logger, IAsyncPolicy<HttpResponseMessage> policy)
        {
            _configuration = configuration;
            _logger = logger;
            _policy = policy;
            httpClient.BaseAddress = new Uri(_configuration["coingeckoAPI"]);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _client = httpClient;
        }
        //public async Task<ExchangeRate> GetCryptoData(Exchange exchangeInfo, string currency, string currencyKind, DateTime histDate)

        public async Task<List<ExchangeRate>> GetCryptoData(ExchangeInformation exchangeInfo, List<DateTime> histDate)
        {
            List<ExchangeRate> exchangeRates = new List<ExchangeRate>();
            foreach (var date in histDate)
            {
                try
                {

                    var response = await GetCryptoData(exchangeInfo, date);

                    if (response != null)
                    {
                        if (response.Date == date.Date)
                        {
                            exchangeRates.Add(response);
                        }
                    }
                }
                catch (HttpRequestException rex)
                {
                    _logger.LogWarning(rex.Message);
                }
            }
            return exchangeRates;
        }

        public async Task<ExchangeRate> GetCryptoData(ExchangeInformation exchangeInfo, DateTime histDate)
        {

            var UTCDateFrom = histDate;
            var UTCDateTo = UTCDateFrom;
            var uriPath = string.Empty;
            switch (exchangeInfo.Kind?.ToUpper())
            {
                case "CRYPTO":
                    {
                        uriPath = $"coins/{exchangeInfo.ExchangeSymbol}/history?date={UTCDateFrom.ToString("dd-MM-yyyy")}";
                        break;
                    }
                default:
                    throw new NotImplementedException($"API does not support looking up exchange data for currency kind: {exchangeInfo.Kind}");
            }



            var coinInfo = await _policy.ExecuteAsync(() => _client.GetAsync(uriPath));
            //var coinInfo = await _client.GetAsync(uriPath);

            switch (coinInfo.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    {

                        var toCoinJsonInfo = JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement;

                        //Get market data
                        decimal marketData = 0m;
                        if (!string.IsNullOrEmpty(exchangeInfo.ExchangeCurrency))
                        {
                            marketData = toCoinJsonInfo.GetProperty("market_data").GetProperty("current_price").GetProperty(exchangeInfo.ExchangeCurrency.ToLower()).GetDecimal();
                        }

                        var marketValues = new ExchangeRate()
                        {
                            Date = histDate.Date,
                            Open = marketData,
                            High = marketData,
                            Low = marketData,
                            Close = marketData,
                            DataSource = $"{_client.BaseAddress}{uriPath}",
                            ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                            Symbol = exchangeInfo.Symbol,
                            LowHighAverage = marketData,
                            OpenCloseAverage = marketData
                            //IsAccurate = true
                        };
                        return marketValues;

                    }

                case System.Net.HttpStatusCode.NotFound:
                    {
                        return new ExchangeRate();
                        // throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase} for exchange {exchangeInfo.ExchangeName} when asking for data on {exchangeInfo.Symbol}"); //null;
                    }

                case System.Net.HttpStatusCode.TooManyRequests:
                    {

                        throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                    }
                default:
                    return new ExchangeRate();
                    //throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;

            }

        }

        //public async Task<Tuple<List<ExchangeRate>, List<DateTime>>> GetCryptoDataRange(Exchange exchangeInfo, string currency, string currencyKind, DateTime fromDate, DateTime toDate)
        public async Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate)
        {
            var toDate = DateTime.UtcNow.Date;
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var epochTo = toDate.Date == DateTime.UtcNow.Date ? (int)(toDate - epochStart).TotalSeconds : (int)(toDate.AddDays(1) - epochStart).TotalSeconds;
            //var epochTo = (int)(toDate.AddDays(1) - epochStart).TotalSeconds;
            var epochFrom = (int)(fromDate - epochStart).TotalSeconds;
            var uriPath = string.Empty;


            var exChangeRates = new List<ExchangeRate>();


            switch (exchangeInfo.Kind?.ToUpper())
            {
                case "CRYPTO":
                    {
                        uriPath = $"coins/{exchangeInfo.ExchangeSymbol}/market_chart/range?vs_currency={exchangeInfo.ExchangeCurrency}&from={epochFrom}&to={epochTo}";
                        break;
                    }
                default:
                    throw new NotImplementedException($"API does not support looking up exchange data for currency kind: {exchangeInfo.Kind}");
            }


            var coinInfo =  await _policy.ExecuteAsync(() => _client.GetAsync(uriPath));


            switch (coinInfo.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    {

                        var toCoinJsonInfo = JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement;

                        //Get market data
                        var priceData = toCoinJsonInfo.GetProperty("prices");

                        var lastDate = DateTime.MinValue;
                        var exChangeRatesTemp = new List<ExchangeRate>();
                        foreach (JsonElement price in priceData.EnumerateArray())
                        {
                            var epoch = price[0].GetInt64();
                            var value = price[1].GetDecimal();

                            //convert epoch to datetime

                            DateTime date = new DateTime(1970, 1, 1, 0, 0, 0, 0); //from start epoch time
                            date = date.AddMilliseconds(epoch); //add the seconds to the start DateTime

                            //Data granularity is automatic (cannot be adjusted)
                            //1 day from query time = 5 minute interval data
                            //1 - 90 days from query time = hourly data
                            //above 90 days from query time = daily data(00:00 UTC)
                            exChangeRatesTemp.Add(new ExchangeRate()
                            {
                                Date = date.Date,
                                Open = 0,
                                Close = value,
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
                            var openValue = exChangeRatesTemp.Where(y => y.Date == x.Key).First().Close;
                            var closeValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Last().Close;
                            var lowValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Min(r => r.Close);
                            var highValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Max(r => r.Close);
                            var averageValue = exChangeRatesTemp.Where(y => y.Date == x.Key).Average(r => r.Close);

                            //return new ExchangeRate()
                            //{
                            //    Date = x.Key,
                            //    Open = 0,
                            //    Close = exChangeRatesTemp.Where(y => y.Date == x.Key).Average(r => r.Close),
                            //    ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                            //    Symbol = exchangeInfo.Symbol,
                            //    DataSource = new Uri($"{_client.BaseAddress}{uriPath}")

                            //};

                            return new ExchangeRate()
                            {
                                Date = x.Key,
                                Open = openValue,
                                Close = closeValue,
                                High = highValue,
                                Low = lowValue,
                                ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                Symbol = exchangeInfo.Symbol,
                                DataSource = $"{_client.BaseAddress}{uriPath}"

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
                                x.Open = open > 0 ? open : x.Close;
                                x.OpenCloseAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.Open), Convert.ToDecimal(x.Close)), 2);
                                x.LowHighAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.Low), Convert.ToDecimal(x.High)), 2);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation(ex.Message);
                            }

                        });



                        return exChangeRates;

                    }
                case System.Net.HttpStatusCode.NotFound:
                    {
                        return new List<ExchangeRate>();
                        //throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase} for exchange {exchangeInfo.ExchangeName} when asking for data on {exchangeInfo.Symbol}"); //null;
                    }

                case System.Net.HttpStatusCode.TooManyRequests:
                    {

                        throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                    }
                default:
                    {
                        return new List<ExchangeRate>();
                        //Console.WriteLine(coinInfo.StatusCode);
                        //throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                    }


            }


        }

        public async Task<List<ExchangeInformation>> GetSupportedSymbols(int total)
        {
            List<ExchangeInformation> returnValues = new List<ExchangeInformation>();
            var totalPages = Math.Ceiling(total / 100m);
            for (int i = 1; i <= totalPages; i++)
            {
                var uriPath = $"coins//markets?vs_currency=usd&order=market_cap_desc&per_page=100&page={i}";
                var coinInfo = await _client.GetAsync(uriPath);
                var content = JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement;

                var result = content.EnumerateArray().Select(x => new ExchangeInformation()
                {
                    Kind = "crypto",
                    Symbol = x.GetProperty("symbol").GetString(),
                    ExchangeName = "CoinGecko",
                    ExchangeSymbol = x.GetProperty("id").GetString(),
                    ExchangeCurrency = "aud"
                }).ToList();

                returnValues.AddRange(result);
            }

            //check for duplicate symbols; if they exist we will remove them as we don't want that
            var dupes = returnValues.GroupBy(x => x.Symbol).Where(c => c.Count() > 1).SelectMany(x => x.ToList()).ToList();
            var dedupedList = returnValues.Except(dupes).ToList();



            return dedupedList;






        }
    }
}
