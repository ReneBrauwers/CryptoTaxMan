using Common.Interfaces;
using Common.Models;
using FileHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;

namespace ExchangeAPI.Services
{
    public class YahooFinanceService : IYahooFinance
    {
        private readonly HttpClient _client;
        private IConfiguration 
            _configuration { get; }
        private readonly ILogger<YahooFinanceService> _logger;
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        public YahooFinanceService(HttpClient httpClient, IConfiguration configuration, ILogger<YahooFinanceService> logger, IAsyncPolicy<HttpResponseMessage> policy)
        {
            _configuration = configuration;
            _logger = logger;
            httpClient.BaseAddress = new Uri(_configuration["yahooAPI"]);
            _policy = policy;
            _client = httpClient;
        }
        public async Task<List<ExchangeRate>> GetFinancialData(ExchangeInformation exchangeInfo, List<DateTime> histDate)
        {
            List<ExchangeRate> exchangeRates = new List<ExchangeRate>();
            foreach (var date in histDate)
            {
                try
                {
                    var response = await GetFinancialData(exchangeInfo, date);
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

        public async Task<ExchangeRate> GetFinancialData(ExchangeInformation exchangeInfo, DateTime histDate)
        {

            var uriPath = ConstructUriPath(exchangeInfo.Kind ?? string.Empty, exchangeInfo.ExchangeSymbol ?? string.Empty, exchangeInfo.ExchangeCurrency ?? string.Empty, histDate, histDate);

            var coinInfo = await _policy.ExecuteAsync(() => _client.GetAsync(uriPath));
           // var coinInfo = await _client.GetAsync(uriPath);

            switch (coinInfo.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    {

                        //CSV TO Dynamic
                        using (MemoryStream inStream = new MemoryStream())
                        {

                            await coinInfo.Content.CopyToAsync(inStream);
                            inStream.Seek(0, SeekOrigin.Begin);
                            var marketValue = new ExchangeRate();

                            // Use the stream reader to read the content of uploaded file,
                            // in this case we can assume it is a textual file.

                            using (TextReader reader = new StreamReader(inStream))
                            {
                                var readerText = await reader.ReadToEndAsync();
                                using (var engine = new FileHelperAsyncEngine<ExchangeRateBase>())
                                {
                                    using (engine.BeginReadString(readerText))
                                    {

                                        foreach (var record in engine)
                                        {
                                            //only for the same date
                                            if (histDate.Date == Convert.ToDateTime(record.Date.Date))
                                            {
                                                marketValue = new ExchangeRate()
                                                {
                                                    Date = record.Date,
                                                    Open = record.Open ?? 0,
                                                    High = record.High ?? 0,
                                                    Low = record.Low ?? 0,
                                                    Close = record.Close ?? 0,
                                                    ExchangeCurrency = exchangeInfo.ExchangeCurrency,
                                                    Symbol = exchangeInfo.ExchangeSymbol,
                                                    //IsAccurate = true,
                                                    OpenCloseAverage = decimal.Divide(decimal.Add(record.Open ?? 0, record.Close ?? 0), 2),
                                                    LowHighAverage = decimal.Divide(decimal.Add(record.Low ?? 0, record.High ?? 0), 2),
                                                    DataSource = new Uri($"{_client.BaseAddress}{uriPath}")
                                                };
                                            }
                                        }
                                    }
                                }
                            }


                            return marketValue;

                        }
                    }
                case System.Net.HttpStatusCode.NotFound:
                    {
                        return new ExchangeRate();
                        //throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase} for exchange {exchangeInfo.ExchangeName} when asking for data on {exchangeInfo.Symbol}"); //null;
                    }

                case System.Net.HttpStatusCode.TooManyRequests:
                    {
                                               
                        throw new HttpRequestException($"API returned {coinInfo.StatusCode} {coinInfo.ReasonPhrase}"); //null;
                    }
                default:
                    return new ExchangeRate();


            }

        }

            public async Task<List<ExchangeRate>> GetFinancialDataRange(ExchangeInformation exchangeInfo, DateTime fromDate)
        {
            var toDate = DateTime.UtcNow.Date;
            var uriPath = ConstructUriPath(exchangeInfo.Kind ?? string.Empty, exchangeInfo.ExchangeSymbol ?? string.Empty, exchangeInfo.ExchangeCurrency ?? string.Empty, fromDate, toDate);
            var coinInfo = await _client.GetAsync(uriPath);
            var exChangeRates = new List<ExchangeRate>();
            //List<DateTime> missingDates = new List<DateTime>();

            switch (coinInfo.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    {

                        //CSV TO Dynamic
                        using (MemoryStream inStream = new MemoryStream())
                        {

                            await coinInfo.Content.CopyToAsync(inStream);
                            inStream.Seek(0, SeekOrigin.Begin);
                            var marketValue = new ExchangeRate();

                            // Use the stream reader to read the content of uploaded file,
                            // in this case we can assume it is a textual file.
                            using (TextReader reader = new StreamReader(inStream))
                            {
                                var readerText = await reader.ReadToEndAsync();
                                readerText = readerText.Replace("null", "0");
                                using (var engine = new FileHelperAsyncEngine<ExchangeRateBase>())
                                {
                                    using (engine.BeginReadString(readerText))
                                    {

                                        foreach (var record in engine)
                                        {
                                            var exRate = new ExchangeRate();
                                            try
                                            {

                                                exRate.Date = record.Date;
                                                exRate.Open = record.Open ?? 0;
                                                exRate.Close = record.Close ?? 0;
                                                exRate.Low = record.Low ?? 0;
                                                exRate.High = record.High ?? 0;
                                                exRate.ExchangeCurrency = exchangeInfo.ExchangeCurrency;
                                                exRate.Symbol = exchangeInfo.Symbol;
                                                exRate.OpenCloseAverage = decimal.Divide(decimal.Add(record.Open ?? 0, record.Close ?? 0), 2);
                                                exRate.LowHighAverage = decimal.Divide(decimal.Add(record.Low ?? 0, record.High ?? 0), 2);
                                                exRate.DataSource = new Uri($"{_client.BaseAddress}{uriPath}");
                                                exChangeRates.Add(exRate);
                                            }

                                            catch
                                            {

                                            }



                                        }
                                    }
                                }
                            }





                        }


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
                    return new List<ExchangeRate>();


            }
        }


        private static string ConstructUriPath(string currencyKind, string coin, string currency, DateTime UtcFrom, DateTime UtcTo)
        {
            var epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var epochTo = (UtcTo.Date == DateTime.UtcNow.Date) ? (int)(UtcTo - epochStart).TotalSeconds : (int)(UtcTo.AddDays(1) - epochStart).TotalSeconds;
            // var epochTo = (int)(UtcTo.AddDays(1) - epochStart).TotalSeconds;
            var epochFrom = (int)(UtcFrom - epochStart).TotalSeconds;

            switch (currencyKind.ToUpper())
            {
                case "CRYPTO":
                    {
                        return $"{coin.ToUpper()}-{currency.ToUpper()}?period1={epochFrom}&period2={epochTo}&interval=1d&events=history&includeAdjustedClose=true";

                    }
                case "FIAT":
                    {

                        if (coin.ToUpper() == currency.ToUpper())
                        {
                            return string.Empty;
                        }
                        else
                        {
                            return $"{coin.ToUpper()}{currency.ToUpper()}=X?period1={epochFrom}&period2={epochTo}&interval=1d&events=history&includeAdjustedClose=true";
                        }

                    }
                case "STOCK":
                    {
                        return $"{coin.ToUpper()}?period1={epochFrom}&period2={epochTo}&interval=1d&events=history&includeAdjustedClose=true";

                    }
                default:
                    throw new NotImplementedException($"API does not support looking up exchange data for currency kind: {currencyKind}");
            }
        }
    }
}
