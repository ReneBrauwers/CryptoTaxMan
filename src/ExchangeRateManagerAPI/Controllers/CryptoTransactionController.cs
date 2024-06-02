using CsvHelper.Configuration;
using CsvHelper;
using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Globalization;
using System.Text;
using ExchangeRateManagerAPI.Model;

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
        public async Task<IActionResult> ProcessCryptoUserTransactionsStaging([FromBody] ProcessCryptoUserTransactionsStagingRequest req)
        {

            var taskId = _databaseService.StartProcessCryptoUserTransactionsStagingTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _databaseService.ProcessCryptoUserTransactionsStaging(req.baseCurrency);
            });

            var checkUrl = Url.Action(nameof(CheckProcessCryptoUserTransactionsStagingTask), new { taskId });
            return Accepted(checkUrl);
            //var result = await _databaseService.ProcessCryptoUserTransactionsStaging(req.baseCurrency);
            //if (result.error)
            //{
            // if (result.details != null)
            // {
            //   return BadRequest(new { Error = result.message, Details = result.details });
            // }
            //  else
            //  {
            //      return BadRequest(new { Error = result.message });
            //  }

            // }
            //else
            // {
            //    return Ok(new { Message = $"{result.records} {result.message}" });
            //}


        }

        [HttpGet("ProcessCryptoUserTransactionsStagingStatus/{taskId}")]
        public IActionResult CheckProcessCryptoUserTransactionsStagingTask(string taskId)
        {
            var (isCompleted, result, error) = _databaseService.CheckProcessCryptoUserTransactionsStagingTaskStatus(taskId);

            if (!isCompleted)
            {
                return Accepted(new
                {
                    StatusUrl = Url.Action(nameof(CheckProcessCryptoUserTransactionsStagingTask), new { taskId }),
                    StatusMessage = _databaseService.StatusMessage
                });
            }

            if (error != null)
            {
                return StatusCode(500, new { ErrorMessage = error.Message, Exception = error.ToString() });
            }

            return Ok(result);
        }

        [HttpPost("GetTaxReportDetails")]
        public async Task<IActionResult> GetTaxReportDetails(TaxSummaryReportRequest req)
        {

            
            try
            {
                var result = await _databaseService.GetTaxReportDetails(req.taxYear, req.capitalGainTaxPercentage);
               
                if (req.formatAsCSV)
                {
                   
                    var csv = new StringBuilder();
                    csv.AppendLine("TaxYear,Asset,SellDate,QuantitySold,QuantityRemaining ,SellRecordSequenceNr,SellExchangeRate,SaleProceeds,BuyDate,BuyExchangeRate,BuyRecordSequenceNr,CapitalGains,IsDiscounted,TotalHoldingDays,CapitalGainTaxPercentage,TaxesDue,TaxCurrency");
                    foreach (var item in result)
                    {
                        csv.AppendLine($"{item.TaxYear},{item.Asset},{item.SellDate},{item.QuantitySold},{item.QuantityRemaining},{item.SellRecordSequenceNr},{item.SellExchangeRate},{item.SaleProceeds},{item.BuyDate},{item.BuyExchangeRate},{item.BuyRecordSequenceNr},{item.CapitalGains},{item.IsDiscounted},{item.TotalHoldingDays},{item.CapitalGainTaxPercentage},{item.TaxesDue},{item.TaxCurrency}");
                    }

                    return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "TaxReportDetails.csv");
                }

                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
            
          
        }

        [HttpPost("GetTaxReportSummary")]
        public async Task<IActionResult> GetTaxReportSummary(TaxSummaryReportRequest req)
        {
            try
            {
                
                var result = await _databaseService.GetTaxReportSummary(req.taxYear, req.capitalGainTaxPercentage);
                //if req.formatAsCSV is true, return the result as a CSV file
                if (req.formatAsCSV)
                {
                    var csv = new StringBuilder();
                    csv.AppendLine("TaxYear,TotalSaleProceeds,TotalCapitalGains,CapitalGainTaxPercentage,TaxesDue,TaxCurrency");
                    foreach (var item in result)
                    {
                        csv.AppendLine($"{item.TaxYear},{item.TotalSaleProceeds},{item.TotalCapitalGains},{item.CapitalGainTaxPercentage},{item.TaxesDue},{item.TaxCurrency}");
                    }

                    return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "TaxReportSummary.csv");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }


        }
    }
}
