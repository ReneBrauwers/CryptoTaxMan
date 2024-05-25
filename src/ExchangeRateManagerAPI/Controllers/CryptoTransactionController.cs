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
            var result = await _databaseService.InsertCryptoUserTransactionsStaging(transactions);
            if(result.error)
            {
                return BadRequest(new { Error = result.message});
            }
            // For this example, we're just returning the count of transactions
            return Ok(new { Message = $"{result.records} {result.message}" });
        }

        [HttpPost("BulkImportTradingPairInformation")]
        public async Task<IActionResult> BulkImportTradingPairInformation(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Upload a valid file.");
            }

            //store file json contents into a list of TradingPairInformation
            var exchangeInfo = System.Text.Json.JsonSerializer.Deserialize<List<ExchangeInformation>>(file.OpenReadStream());

            if (exchangeInfo is not null && exchangeInfo.Count() > 0)
            {

                // Process your transactions here
                var result = await _databaseService.InsertTradingPairInformation(exchangeInfo);
                if (result.error)
                {
                    return BadRequest(new { Error = result.message });
                }

                return Ok(new { Message = $"{result.records} {result.message}" });
            }

            return BadRequest("No data found in the file.");
             
           
        }

        [HttpPost("ProcessCryptoUserTransactionsStaging")]
        public async Task<IActionResult> ProcessCryptoUserTransactionsStaging()
        {
            var result = await _databaseService.ProcessCryptoUserTransactionsStaging();
            if (result.error)
            {
               // if (result.details != null)
               // {
                    return BadRequest(new { Error = result.message, Details = result.details });
               // }
              //  else
              //  {
              //      return BadRequest(new { Error = result.message });
              //  }
 
            }
            else
            {
                return Ok(new { Message = $"{result.records} {result.message}" });
            }

             
        }
    }
}
