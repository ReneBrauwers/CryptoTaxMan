using CsvHelper.Configuration;
using CsvHelper;
using ExchangeRateManagerAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Models;
using System.Globalization;
using System.Text;
using ExchangeRateManagerAPI.Model;
using FileHelpers;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Http;

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
        public IActionResult ProcessCryptoUserTransactionsStaging([FromBody] ProcessCryptoUserTransactionsStagingRequest req)
        {

            var taskId = _databaseService.StartProcessCryptoUserTransactionsStagingTask(async () =>
            {
                // Adjust to call the new method for executing sequential API calls
                return await _databaseService.ProcessCryptoUserTransactionsStaging(req.baseCurrency);
            });

            var checkUrl = Url.Action(nameof(CheckProcessCryptoUserTransactionsStagingTask), new { taskId });
            return Accepted(checkUrl);
         


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

            //return result include all result items;

            return Ok(new { Message = $"{result.records} {result.message}",Details = result.details });
           
        }

        [HttpPost("GetTaxReportDetails")]
        public async Task<IActionResult> GetTaxReportDetails(TaxSummaryReportRequest req)
        {

            
            try
            {
                var result = await _databaseService.GetTaxReportDetails(req.taxYear, req.capitalGainTaxPercentage);

                //if req.formatAsCSV is true, return the result as a CSV file
                if (req.formatAsCSV)
                {
                    var engine = new FileHelperEngine<TaxReportDetail>();
                    engine.HeaderText = engine.GetFileHeader();
                    var outputString = engine.WriteString(result); // flattenedRecords);
                    return File(Encoding.UTF8.GetBytes(outputString.ToString()), "text/csv", "TaxReportDetails.csv");

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
                    var engine = new FileHelperEngine<TaxReportSummary>();
                    engine.HeaderText = engine.GetFileHeader();
                    var outputString = engine.WriteString(result); // flattenedRecords);
                    return File(Encoding.UTF8.GetBytes(outputString.ToString()), "text/csv", "TaxReportSummary.csv");
                    
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }


        }

        [HttpPost("GetCryptoUserTransactions")]
        public async Task<IActionResult> GetCryptoUserTransactions([FromBody] CryptoUserTransactionsRequest req)
        {
            DateTime startDate = DateTime.MinValue;
            DateTime endDate = DateTime.MaxValue;
            //check that the fromDateString is in the currect format yyyyMMdd
            if (!string.IsNullOrWhiteSpace(req.startDate) && !DateTime.TryParseExact(req.startDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate))
            {
                return BadRequest("Invalid start date format. Please use yyyyMMdd");
            }

            if (!string.IsNullOrWhiteSpace(req.endDate) &&!DateTime.TryParseExact(req.endDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
            {
                return BadRequest("Invalid end date format. Please use yyyyMMdd");
            }

            try
            {
                var result = await _databaseService.GetCryptoUserTransactions(startDate, endDate, req.asset);

                //given the result, calculate the saldo remaining for each transaction for a given asset. When the transaction is a buy, the saldo is increased, when the transaction is a sell, the saldo is decreased.
                foreach (var groupedUserTransactions in result.GroupBy(x=>x.AmountAssetType))
                {
                    decimal saldo = 0;
                    foreach(var item in groupedUserTransactions.OrderBy(x=>x.TransactionDate))
                    {
                        if(item.TransactionType == Shared.Enums.TransactionEventType.buy)
                        {
                            saldo += item.Amount;
                        }
                        else if(item.TransactionType == Shared.Enums.TransactionEventType.sell)
                        {
                            saldo -= item.Amount;
                        }

                        item.InternalNotes = $"{saldo.ToString()} {groupedUserTransactions.Key} remaining";
                    }
                   
                }
                
              

                //if req.formatAsCSV is true, return the result as a CSV file
                if(req.formatAsCSV)
                {
                    var engine = new FileHelperEngine<CryptoUserTransaction>();
                    engine.HeaderText = engine.GetFileHeader();
                    var outputString = engine.WriteString(result); // flattenedRecords);
                    return File(Encoding.UTF8.GetBytes(outputString.ToString()), "text/csv", "CryptoUserTransactions.csv");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("GetCurrentHoldings/{assetName}")]
        public async Task<IActionResult> GetCurrentHoldings([FromRoute] string assetName)
        {
            if(string.IsNullOrWhiteSpace(assetName))
            {
                return BadRequest("Invalid asset name. Please provide a valid asset name.");
            }

            try
            {
                var result = await _databaseService.GetCurrentHoldings(assetName);
                return Content(result, "text/html");
                 
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("GetProfitsReport/{assetName}")]
        public async Task<IActionResult> GetProfitsReport([FromRoute] string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return BadRequest("Invalid asset name. Please provide a valid asset name.");
            }

            try
            {
                var result = await _databaseService.CalculateProfits(assetName);
                return Ok(result);

            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("GetBreakEvenPoint/{assetName}")]
        public async Task<IActionResult> GetBreakEvenPoint([FromRoute] string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName))
            {
                return BadRequest("Invalid asset name. Please provide a valid asset name.");
            }

            try
            {
                var result = await _databaseService.CalculateBreakEvenPrice(assetName);
                return Ok(result);

            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
        //CalculateBreakEvenPrice
    }
}
