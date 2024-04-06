using CsvHelper.Configuration;
using CsvHelper;
using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Globalization;
using System.Text;

namespace ExchangeRateManagerAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class CryptoTransactionController : Controller
    {

        private readonly DatabaseService _databaseService;
        private readonly ILogger<CryptoTransactionController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _config;

        public CryptoTransactionController(IConfiguration config,  ILogger<CryptoTransactionController> logger, IWebHostEnvironment environment, DatabaseService databaseService)
        {
            _logger = logger;
            _environment = environment;
            _databaseService = databaseService;
            _config = config;
        }

        [HttpPost("BulkImportTransactions")]
        public async Task<IActionResult> BulkImportTransactions(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Upload a valid CSV file.");
            }

            var transactions = new List<CryptoUserTransactionStaging>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                PrepareHeaderForMatch = args => args.Header.ToLower(),
            };

            using (var reader = new StreamReader(file.OpenReadStream()))
            using (var csv = new CsvReader(reader, config))
            {
                csv.Context.RegisterClassMap<CryptoUserTransactionStagingMap>();
                transactions = csv.GetRecords<CryptoUserTransactionStaging>().ToList();
            }

            // Process your transactions here
            var result = await _databaseService.InsertCryptoUserTransactions(transactions);
            if(result.error)
            {
                return BadRequest(new { Error = result.message});
            }
            // For this example, we're just returning the count of transactions
            return Ok(new { Message = $"{result.records} {result.message}" });
        }

        //[HttpGet]
        //public async Task<IActionResult> Get([FromQuery] DateTime transactionDate, [FromQuery] string currencyIn, [FromQuery] string exchangeCurrency = "aud", [FromQuery] bool nearestMatch = true)
        //{
        //    ExchangeRate? result = null;
        //    int maxTimeTravelAllowed = _config.GetValue<int>("maxNearestMatchRange", 1);
        //    do
        //    {
        //        result = await _databaseService.FetchExchangeRateInformation(transactionDate, currencyIn.ToLower(), exchangeCurrency.ToLower());
        //        if (result is not null && nearestMatch)
        //        {
        //            break;
        //        }

        //        if (result is null && nearestMatch)
        //        {
        //            if(maxTimeTravelAllowed == 0)
        //            {
        //                break;
        //            }
        //            transactionDate = transactionDate.AddDays(-1);
        //            maxTimeTravelAllowed--;
        //        }
        //    } while (nearestMatch);


        //    if (result is null)
        //    {
        //        return NotFound();
        //    }

        //    return Ok(result);
        //}
    }
}
