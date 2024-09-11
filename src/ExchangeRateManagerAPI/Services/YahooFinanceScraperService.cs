using ExchangeRateManagerAPI.Interfaces;
using FileHelpers;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using Polly;
using Shared.Models;
using OpenQA.Selenium.Chrome;
using SeleniumExtras.WaitHelpers;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Components.Web;

namespace ExchangeRateManagerAPI.Services
{
    public class YahooFinanceScraperService : IYahooFinanceScaper, IDisposable
    {

        private IConfiguration _configuration { get; }
        private readonly ILogger<IYahooFinanceScaper> _logger;
        private readonly IWebDriver _driver;
        private readonly WebDriverWait _wait;

        public YahooFinanceScraperService(IConfiguration configuration, ILogger<YahooFinanceScraperService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            _driver = new ChromeDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        }





        public async Task<List<ExchangeRate>> GetFinancialDataRange(ExchangeInformation exchangeInfo, DateTime fromDate)
        {
            var toDate = DateTime.UtcNow.Date;
            var allExchangeRates = new List<ExchangeRate>();

            var currentFromDate = fromDate;
            while (currentFromDate < toDate)
            {
                var currentToDate = new DateTime(Math.Min(currentFromDate.AddMonths(1).Ticks, toDate.Ticks));

                var monthlyRates = await FetchMonthlyData(exchangeInfo, currentFromDate, currentToDate);
                allExchangeRates.AddRange(monthlyRates);

                currentFromDate = currentToDate.AddDays(1);

                // Optional: Add a delay between requests to avoid rate limiting
                await Task.Delay(1000);
            }

            return allExchangeRates;
        }
        private async Task<List<ExchangeRate>> FetchMonthlyData(ExchangeInformation exchangeInfo, DateTime fromDate, DateTime toDate)
        {           
         

            var url = ConstructUrl(exchangeInfo.ExchangeSymbol, exchangeInfo.ExchangeCurrency, fromDate, toDate, (exchangeInfo.Kind == "fiat"));

            _driver.Navigate().GoToUrl(url);
           
            // Wait for the table to load with retry mechanism
            
                await WaitForTableWithRetry();
           

            // Extract data from the table
            var exchangeRates = new List<ExchangeRate>();

            // Use JavaScript to get table rows
            var rows = (IReadOnlyCollection<IWebElement>)((IJavaScriptExecutor)_driver).ExecuteScript(@"
        var table = document.querySelector('table[data-test=""historical-prices""]');
        return table ? table.querySelectorAll('tbody tr') : [];
    ");

            foreach (var row in rows)
            {
                var columns = row.FindElements(By.TagName("td"));

                if (columns.Count >= 7)
                {
                    var exchangeRate = new ExchangeRate();

                    try
                    {
                        



                        exchangeRate.Date = ParseDate(columns[0].Text);
                        exchangeRate.Open = ParseDecimal(columns[1].Text);
                        exchangeRate.High = ParseDecimal(columns[2].Text);
                        exchangeRate.Low = ParseDecimal(columns[3].Text);
                        exchangeRate.Close = ParseDecimal(columns[4].Text);
                        //exchangeRate.AdjustedClose = ParseDecimal(columns[5].Text);
                        //exchangeRate.Volume = ParseLong(columns[6].Text);
                        exchangeRate.ExchangeCurrency = exchangeInfo.ExchangeCurrency;
                        exchangeRate.Symbol = exchangeInfo.Symbol;
                        exchangeRate.OpenCloseAverage = (ParseDecimal(columns[1].Text) + ParseDecimal(columns[4].Text)) / 2;
                        exchangeRate.LowHighAverage = (ParseDecimal(columns[2].Text) + ParseDecimal(columns[3].Text)) / 2;
                        exchangeRate.DataSource = "YahooFinance";


                        exchangeRates.Add(exchangeRate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to parse row: {string.Join(", ", columns.Select(c => c.Text))}");
                        throw new Exception($"Failed to parse row: {string.Join(", ", columns.Select(c => c.Text))}");
                    }
                }
            }

            return exchangeRates;
        }

        private async Task WaitForTableWithRetry(int maxAttempts = 3, int delayMs = 5000)
        {
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    await Task.Delay(delayMs); // Wait for the page to load

                    // Use JavaScript to check if the table exists and has rows
                    var tableExists = (bool)((IJavaScriptExecutor)_driver).ExecuteScript(@"
                var table = document.querySelector('table[data-test=""historical-prices""]');
                return !!(table && table.querySelectorAll('tbody tr').length > 0);
            ");

                    if (tableExists)
                    {
                        return; // Table found, exit the method
                    }
                
                }
                catch (WebDriverException)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        throw; // Rethrow the exception on the last attempt
                    }
                }

                _logger.LogWarning($"Attempt {attempt + 1} failed. Retrying...");
                //Console.WriteLine($"Attempt {attempt + 1} failed. Retrying...");
            }          
            var pageUrl = _driver.Url;           
            _logger.LogInformation($"Urk: {pageUrl}");
            throw new NotSupportedException($"Scraping yielded no results for page {pageUrl} .");
        }
        private string ConstructUrl(string symbol, string currency, DateTime fromDate, DateTime toDate, bool isFiat)
        {

            long period1 = ((DateTimeOffset)fromDate).ToUnixTimeSeconds();
            long period2 = ((DateTimeOffset)toDate).ToUnixTimeSeconds();

            if (!isFiat)
            {
                return $"{_configuration["yahooAPI"]}{symbol}-{currency}/history/?period1={period1}&period2={period2}&interval=1d&filter=history&frequency=1d&includeAdjustedClose=true";
            }
            else
            {
                return $"{_configuration["yahooAPI"]}{symbol.ToUpper()}{currency.ToUpper()}=X/history/?period1={period1}&period2={period2}&interval=1d&filter=history&frequency=1d&includeAdjustedClose=true";
            }
        }

        
        private decimal ParseDecimal(string value)
        {
            if(value == "-")
            {
                return 0;
            }

            return decimal.Parse(value.Replace(",", ""), CultureInfo.InvariantCulture);
        }

        private DateTime ParseDate(string dateString)
        {
            string[] formats = {
        "dd MMM yyyy",    
        "dd MMMM yyyy",   
        "d MMM yyyy",    
        "d MMMM yyyy",    
        "MMM dd, yyyy",  
    };

            if (DateTime.TryParseExact(dateString, formats, new CultureInfo("en-US"), DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // If the above fails, try a more flexible parsing approach
            CultureInfo[] cultures = { new CultureInfo("en-US"), new CultureInfo("en-AU") };

            foreach (var culture in cultures)
            {
                if (DateTime.TryParseExact(dateString, formats, culture, DateTimeStyles.None, out result))
                {
                    return result;
                }
            }

            // If the above fails, try a more flexible parsing approach with both cultures
            foreach (var culture in cultures)
            {
                if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out result))
                {
                    return result;
                }
            }

            throw new FormatException($"Unable to parse date: {dateString}");
        }

        private long ParseLong(string value)
        {
            return long.Parse(value.Replace(",", ""), CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            _driver?.Quit();
            _driver?.Dispose();
        }
    }
}
