using Polly;
using Shared.Models;
using Shared.Extensions;
using System.Text.Json;
using ExchangeRateManagerAPI.Interfaces;

namespace ExchangeRateManagerAPI.Services
{
    public class LiveCoinWatchService : ILiveCoinWatch
    {
        private readonly HttpClient _client;
        private IConfiguration _configuration { get; }

        private readonly ILogger<LiveCoinWatchService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;

        public LiveCoinWatchService(HttpClient httpClient, IConfiguration configuration, ILogger<LiveCoinWatchService> logger, IAsyncPolicy<HttpResponseMessage> policy)
        {
            _configuration = configuration;
            _logger = logger;
            _policy = policy;
            var apiKey = _configuration["liveCoinWatchAPIKey"];
            httpClient.BaseAddress = new Uri(_configuration["liveCoinWatchAPI"]);
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _client = httpClient;
        }
        //public async Task<ExchangeRate> GetCryptoData(Exchange exchangeInfo, string currency, string currencyKind, DateTime histDate)


        public async Task<List<ExchangeRate>> GetCryptoDataRange(ExchangeInformation exchangeInfo, DateTime fromDate)
        {

            List<ExchangeRate> marketValues = new List<ExchangeRate>();

            var uriPath = string.Empty;
            switch (exchangeInfo.Kind?.ToUpper())
            {
                case "CRYPTO":
                    {
                        uriPath = "coins/single/history";
                        break;
                    }
                default:
                    throw new NotImplementedException($"API does not support looking up exchange data for currency kind: {exchangeInfo.Kind}");

            }

            DateTime yesterday = DateTime.UtcNow.AddDays(-1);

            //how many months are there in the range taking fromdate upto yesterday
            var months = (yesterday.Year - fromDate.Year) * 12 + yesterday.Month - fromDate.Month;

            //foreach month, get the data
            for (int i = 0; i <= months; i++)
            {
                var histDate = fromDate.AddMonths(i);

                //after the second iteration ensure that the histDate starts from the first day of the month
                if (i > 0)
                {
                    histDate = new DateTime(histDate.Year, histDate.Month, 1);
                }

                

                //create range use unix timestamp in milliseconds
                var fromUnixTimeStamp = ((DateTimeOffset)histDate).ToUnixTimeMilliseconds();

                //get the max date in the month histDate and covert to to unix timestamp in milliseconds
                var lastDayOfMonth = new DateTime(histDate.Year, histDate.Month, DateTime.DaysInMonth(histDate.Year, histDate.Month));
                var toUnixTimeStamp = ((DateTimeOffset)lastDayOfMonth).ToUnixTimeMilliseconds();



                //create json payload for the request to post
                var jsonPayload = new
                {
                    code = exchangeInfo.ExchangeSymbol.ToUpper(),
                    currency = exchangeInfo.ExchangeCurrency.ToUpper(),
                    start = fromUnixTimeStamp,
                    end = toUnixTimeStamp,
                    meta = false
                };


                //post the request
                var coinInfo = await _policy.ExecuteAsync(() => _client.PostAsJsonAsync(uriPath, jsonPayload));



                switch (coinInfo.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        {

                            var toCoinJsonInfo = JsonDocument.Parse(await coinInfo.Content.ReadAsStringAsync()).RootElement;

                            //check if the history array is empty
                            if (toCoinJsonInfo.GetProperty("history").GetArrayLength() == 0)
                            {
                                break;
                            }

                            //map all history data into a list, convert the date from unix timestamp to datetime
                            var historyData = toCoinJsonInfo.GetProperty("history").EnumerateArray().Select(x => new ExchangeRate()
                            {
                                Date = DateTimeOffset.FromUnixTimeMilliseconds(x.GetProperty("date").GetInt64()).DateTime,
                                Open = x.GetProperty("rate").GetDecimal(),
                                Close = x.GetProperty("rate").GetDecimal(),
                                High = x.GetProperty("rate").GetDecimal(),
                                Low = x.GetProperty("rate").GetDecimal(),
                                ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                Symbol = exchangeInfo.Symbol,
                                DataSource = "LiveCoinWatch"
                            }).ToList();


                            //group all entries in histyoryData for the current date
                            var groupedHistoryData = historyData.GroupBy(x => x.Date.Date).Select(x => new ExchangeRate()
                            {
                                Date = x.Key,
                                Open = x.First().Open,
                                Close = x.Last().Close,
                                High = x.Max(y => y.High),
                                Low = x.Min(y => y.Low),
                                OpenCloseAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.First().Open), Convert.ToDecimal(x.Last().Close)), 2),
                                LowHighAverage = decimal.Divide(decimal.Add(Convert.ToDecimal(x.Min(y => y.Low)), Convert.ToDecimal(x.Max(y => y.High))), 2),
                                ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                Symbol = exchangeInfo.Symbol,
                                DataSource = "LiveCoinWatch"
                            }).ToList();

                            //for all entries in groupedHistoryData remove the max date entry in case the current UTC Date is the max date entry
                            if (groupedHistoryData.Any(x => x.Date.Date == DateTime.UtcNow.Date))
                            {
                                groupedHistoryData.RemoveAll(x => x.Date.Date == DateTime.UtcNow.Date);
                            }


                            marketValues.AddRange(groupedHistoryData);
                            break;

                        }

                    case System.Net.HttpStatusCode.NotFound:
                        {
                            // return new ExchangeRate();
                            throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase} for exchange {exchangeInfo.ExchangeName} when asking for data on {exchangeInfo.Symbol}"); //null;
                        }

                    case System.Net.HttpStatusCode.TooManyRequests:
                        {

                            throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                        }
                    default:
                        break;
                        //throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;

                }

            }

            return marketValues;
        }
    
    }
}
